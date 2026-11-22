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
    IAiAssessmentRepository assessments)
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

    public async Task<IReadOnlyList<ApplicationDto>> ListAsync(Caller caller, bool all, CancellationToken ct)
    {
        var list = all && caller.IsStaff
            ? await applications.ListAllAsync(200, ct)
            : await applications.ListForApplicantAsync(caller.UserId, ct);
        return list.Select(ApplicationDto.From).ToList();
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

        var errors = FormValidator.Validate(service, values, values.Keys, caller.Language);
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
        return await ValidateAllAsync(app, caller.Language, ct);
    }

    /// <summary>
    /// Submits a draft, or sends back an application the AI pre-review asked to complete.
    /// Either way the whole application is validated again first.
    /// </summary>
    public async Task<ApplicationDto> SubmitAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await GetOwnedAsync(caller, id, ct);
        var errors = await ValidateAllAsync(app, caller.Language, ct);
        if (errors.Count > 0) throw new ValidationException(errors);

        var service = await DefinitionForAsync(app, ct);
        var reference = service.ReferenceField is null ? null : app.GetData().GetValueOrDefault(service.ReferenceField);

        if (app.Status == ApplicationStatus.InformationRequested)
        {
            app.ProvideInformation(reference);
            await notifier.NotifyAsync(app, new LocalizedText(
                "Thank you. We received the information and are checking your application again.",
                "شكرًا لك. استلمنا المعلومات ونعيد التحقق من طلبك."), ct);
            await applications.SaveChangesAsync(ct);
            await workflow.SignalInformationProvidedAsync(app.Id, ct);
        }
        else
        {
            app.Submit(reference);
            await notifier.NotifyAsync(app, new LocalizedText("Your application was submitted.", "تم تقديم طلبك."), ct); // staged in the outbox
            await applications.SaveChangesAsync(ct);                                                                      // commits change + event

            // Hand over to the process workflow for this application's version (standard or AI-assisted).
            await workflow.StartApplicationProcessAsync(app.Id, app.WorkflowVersion, ct);
        }

        return ApplicationDto.From(await applications.ReadAsync(app.Id, ct) ?? app);
    }

    private async Task<List<FieldError>> ValidateAllAsync(ServiceApplication app, string lang, CancellationToken ct)
    {
        var service = await DefinitionForAsync(app, ct);
        var data = app.GetData();
        var errors = FormValidator.Validate(service, data, lang: lang);
        errors.AddRange(rules.Where(r => r.ServiceCode == service.Code).SelectMany(r => r.Validate(data, lang)));

        if (service.ReferenceField is { } refField && data.GetValueOrDefault(refField) is { Length: > 0 } reference &&
            await applications.ReferenceInUseAsync(service.Code, reference, app.Id, ct))
        {
            errors.Add(new FieldError(refField, Localized.Pick(lang,
                "This name is already reserved or under review.", "هذا الاسم محجوز أو قيد المراجعة.")));
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
