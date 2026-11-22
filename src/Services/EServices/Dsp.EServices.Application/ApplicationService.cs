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
    StatusNotifier notifier)
{
    public IReadOnlyList<ServiceSummaryDto> ListServices() =>
        catalog.GetAll().Select(ServiceSummaryDto.From).ToList();

    public ServiceDefinition GetService(string code) =>
        catalog.Find(code) ?? throw new NotFoundException($"Service '{code}' was not found.");

    public async Task<ApplicationDto> CreateDraftAsync(Caller caller, string serviceCode, CancellationToken ct)
    {
        var service = GetService(serviceCode);

        // Pre-fill from the signed-in profile (UAE PASS data) so users don't retype what we already know.
        var prefill = service.AllFields
            .Where(f => f.PrefillFrom is not null && caller.Profile.ContainsKey(f.PrefillFrom))
            .ToDictionary(f => f.Key, f => caller.Profile[f.PrefillFrom!]);

        var app = ServiceApplication.Create(service.Code, service.Name, service.WorkflowVersion, caller.UserId,
            caller.Name, service.Fee, service.Currency, prefill);
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

    /// <summary>Saves some fields and returns errors for just those fields. Valid values are kept either way.</summary>
    public async Task<SaveResultDto> SaveDataAsync(Caller caller, Guid id, IReadOnlyDictionary<string, string?> values,
        CancellationToken ct)
    {
        var app = await GetOwnedAsync(caller, id, ct);
        var service = GetService(app.ServiceCode);

        var errors = FormValidator.Validate(service, values, values.Keys);
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

    public async Task<ApplicationDto> SubmitAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await GetOwnedAsync(caller, id, ct);
        var errors = await ValidateAllAsync(app, ct);
        if (errors.Count > 0) throw new ValidationException(errors);

        var service = GetService(app.ServiceCode);
        var reference = service.ReferenceField is null ? null : app.GetData().GetValueOrDefault(service.ReferenceField);
        app.Submit(reference);
        await applications.SaveChangesAsync(ct);
        await notifier.NotifyAsync(app, "Your application was submitted.", ct);

        // Hand over to the core process workflow (review, payment, certificate).
        await workflow.StartApplicationProcessAsync(app.Id, ct);

        return ApplicationDto.From(await applications.ReadAsync(app.Id, ct) ?? app);
    }

    private async Task<List<FieldError>> ValidateAllAsync(ServiceApplication app, CancellationToken ct)
    {
        var service = GetService(app.ServiceCode);
        var data = app.GetData();
        var errors = FormValidator.Validate(service, data);
        errors.AddRange(rules.Where(r => r.ServiceCode == service.Code).SelectMany(r => r.Validate(data)));

        if (service.ReferenceField is { } refField && data.GetValueOrDefault(refField) is { Length: > 0 } reference &&
            await applications.ReferenceInUseAsync(service.Code, reference, app.Id, ct))
        {
            errors.Add(new FieldError(refField, "This name is already reserved or under review."));
        }

        return errors;
    }

    private async Task<ServiceApplication> GetOwnedAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await applications.GetAsync(id, ct) ?? throw new NotFoundException("Application not found.");
        if (!app.IsOwnedBy(caller.UserId)) throw new NotFoundException("Application not found.");
        return app;
    }

    private async Task<ServiceApplication> ReadVisibleAsync(Caller caller, Guid id, CancellationToken ct)
    {
        var app = await applications.ReadAsync(id, ct) ?? throw new NotFoundException("Application not found.");
        if (!caller.IsStaff && !app.IsOwnedBy(caller.UserId)) throw new NotFoundException("Application not found.");
        return app;
    }
}
