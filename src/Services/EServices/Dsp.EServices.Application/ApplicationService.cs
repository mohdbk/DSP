using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Dsp.EServices.Domain.Rules;
using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>Citizen-facing use cases. Used by the form view directly and by the AI agent as tools.</summary>
public sealed class ApplicationService(
    IServiceCatalog catalog,
    IApplicationRepository applications,
    IEnumerable<IServiceRules> rules,
    IWorkflowEngine workflow,
    StatusNotifier notifier,
    IAiAssessmentRepository assessments,
    IMessages messages)
{
    public async Task<IReadOnlyList<ServiceSummaryDto>> ListServicesAsync(CancellationToken ct) =>
        (await catalog.ListPublishedAsync(ct)).Select(ServiceSummaryDto.From).ToList();

    /// <summary>The published service, or a given version of it (to show an application as it was filled in).</summary>
    public async Task<ServiceDefinition> GetServiceAsync(string code, int? version, CancellationToken ct) =>
        (version is { } v ? await catalog.FindVersionAsync(code, v, ct) : await catalog.FindPublishedAsync(code, ct))
        ?? throw new NotFoundException("errors.service.notFound");

    public async Task<ApplicationDto> CreateDraftAsync(Caller caller, string serviceCode, CancellationToken ct)
    {
        var service = await GetServiceAsync(serviceCode, null, ct);

        // Pre-fill from the signed-in profile (UAE PASS data) so users don't retype what we already know.
        var prefill = service.AllFields
            .Where(f => f.PrefillFrom is not null && caller.Profile.ContainsKey(f.PrefillFrom))
            .ToDictionary(f => f.Key, f => caller.Profile[f.PrefillFrom!]);

        var app = ServiceApplication.Create(service.Code, service.Name, service.WorkflowVersion, caller.UserId,
            caller.Name, service.Fee, service.Currency, prefill, service.Version);
        await applications.AddAsync(app, ct);
        await applications.SaveChangesAsync(ct);
        return ApplicationDto.From(app);
    }

    public async Task<ApplicationDto> GetAsync(Caller caller, Guid id, CancellationToken ct) =>
        ApplicationDto.From(await ReadVisibleAsync(caller, id, ct));

    /// <summary>One page of applications: staff see every submitted one (<paramref name="all"/>), others their own.</summary>
    public async Task<PagedList<ApplicationDto>> ListAsync(Caller caller, bool all, ListQuery query, CancellationToken ct)
    {
        var fields = Lists.Applications(Lists.ServiceOptions(await catalog.ListPublishedAsync(ct)), messages);
        var page = await applications.QueryAsync(all && caller.IsStaff ? null : caller.UserId, fields, query, ct);
        return page.Map(ApplicationDto.From);
    }

    /// <summary>The published services, searched and paged (the catalogue).</summary>
    public async Task<PagedList<ServiceSummaryDto>> ListServicesAsync(ListQuery query, CancellationToken ct)
    {
        var fields = new ListFields<ServiceSummaryDto>(q => q.OrderBy(s => s.Code))
            .Text("code", s => s.Code)
            .Text("name", s => messages.Pick(s.Name))
            .Text("description", s => messages.Pick(s.Description))
            .Text("category", s => messages.Pick(s.Category))
            .Number("fee", s => s.Fee);
        return fields.Page(await ListServicesAsync(ct), query);
    }

    /// <summary>The AI pre-review rounds for an application (staff only).</summary>
    public async Task<IReadOnlyList<AiAssessmentDto>> ListAssessmentsAsync(Caller caller, Guid id, CancellationToken ct)
    {
        if (!caller.IsStaff) throw new ForbiddenException("errors.staffOnly");
        var app = await ReadVisibleAsync(caller, id, ct);
        return (await assessments.ListForApplicationAsync(app.Id, ct)).Select(AiAssessmentDto.From).ToList();
    }

    /// <summary>Saves some fields and returns errors for just those fields. Valid values are kept either way.</summary>
    public async Task<SaveResultDto> SaveDataAsync(Caller caller, Guid id, IReadOnlyDictionary<string, string?> values,
        CancellationToken ct)
    {
        var app = await GetOwnedAsync(caller, id, ct);
        var service = await DefinitionForAsync(app, ct);

        var errors = FormValidator.Validate(service, values, messages, values.Keys);
        var valid = values.Where(v => errors.All(e => !string.Equals(e.Field, v.Key, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(v => service.FindField(v.Key)!.Key, v => v.Value);

        if (valid.Count > 0)
        {
            app.UpdateData(valid);
            await applications.SaveChangesAsync(ct);
        }

        return new SaveResultDto(ApplicationDto.From(app), errors);
    }

    /// <summary>Full validation: every field plus the service's business rules.</summary>
    public async Task<IReadOnlyList<FieldError>> ValidateAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await GetOwnedAsync(caller, id, ct);
        return await ValidateAllAsync(app, ct);
    }

    /// <summary>
    /// Submits a draft, or sends back an application the AI pre-review asked to complete.
    /// Either way the whole application is validated again first.
    /// </summary>
    public async Task<ApplicationDto> SubmitAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await GetOwnedAsync(caller, id, ct);
        var errors = await ValidateAllAsync(app, ct);
        if (errors.Count > 0) throw new ValidationException(errors);

        var service = await DefinitionForAsync(app, ct);
        var reference = service.ReferenceField is null ? null : app.GetData().GetValueOrDefault(service.ReferenceField);

        if (app.Status == ApplicationStatus.InformationRequested)
        {
            app.ProvideInformation(reference);
            await notifier.NotifyAsync(app, MessageRef.Of("status.notify.informationReceived"), ct);
            await applications.SaveChangesAsync(ct);
            await workflow.SignalInformationProvidedAsync(app.Id, ct);
        }
        else
        {
            app.Submit(reference);
            await notifier.NotifyAsync(app, MessageRef.Of("status.notify.submitted"), ct); // staged in the outbox
            await applications.SaveChangesAsync(ct);                                      // commits change + event

            // Hand over to the process workflow for this application's version (standard or AI-assisted).
            await workflow.StartApplicationProcessAsync(app.Id, app.WorkflowVersion, ct);
        }

        return ApplicationDto.From(await applications.ReadAsync(app.Id, ct) ?? app);
    }

    private async Task<List<FieldError>> ValidateAllAsync(ServiceApplication app, CancellationToken ct)
    {
        var service = await DefinitionForAsync(app, ct);
        var data = app.GetData();
        var errors = FormValidator.Validate(service, data, messages);
        errors.AddRange(rules.Where(r => r.ServiceCode == service.Code).SelectMany(r => r.Validate(data, messages)));

        if (service.ReferenceField is { } refField && data.GetValueOrDefault(refField) is { Length: > 0 } reference &&
            await applications.ReferenceInUseAsync(service.Code, reference, app.Id, ct))
        {
            errors.Add(new FieldError(refField, messages.Format("validation.referenceInUse")));
        }

        return errors;
    }

    /// <summary>The definition version the application started on (falls back to the published one).</summary>
    private async Task<ServiceDefinition> DefinitionForAsync(ServiceApplication app, CancellationToken ct) =>
        await catalog.FindVersionAsync(app.ServiceCode, app.ServiceVersion, ct)
        ?? await catalog.FindPublishedAsync(app.ServiceCode, ct)
        ?? throw new NotFoundException("errors.service.notFound");

    private async Task<ServiceApplication> GetOwnedAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await applications.GetAsync(id, ct) ?? throw new NotFoundException("errors.application.notFound");
        if (!app.IsOwnedBy(caller.UserId)) throw new NotFoundException("errors.application.notFound");
        return app;
    }

    private async Task<ServiceApplication> ReadVisibleAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await applications.ReadAsync(id, ct) ?? throw new NotFoundException("errors.application.notFound");
        if (!caller.IsStaff && !app.IsOwnedBy(caller.UserId)) throw new NotFoundException("errors.application.notFound");
        return app;
    }
}
