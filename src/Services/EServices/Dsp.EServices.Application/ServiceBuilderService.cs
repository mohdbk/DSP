using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>
/// The no-code service builder: staff create and edit e-services as data, preview them and publish
/// numbered versions. Publishing affects new applications only; those in flight keep their version.
/// </summary>
public sealed class ServiceBuilderService(IServiceVersionRepository versions, IMessages messages, ICurrencies currencies)
{
    public async Task<IReadOnlyList<BuilderServiceSummaryDto>> ListAsync(Caller caller, CancellationToken ct)
    {
        EnsureStaff(caller);
        var all = await versions.ListAllAsync(ct);
        return all.GroupBy(v => v.Code)
            .Select(g =>
            {
                var published = g.FirstOrDefault(v => v.Status == ServiceVersionStatus.Published);
                var draft = g.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft);
                var latest = (draft ?? published ?? g.OrderByDescending(v => v.Version).First()).ToDefinition();
                var updated = g.MaxBy(v => v.UpdatedAt)!;
                return new BuilderServiceSummaryDto(g.Key, latest.Name, latest.Category,
                    published?.Version, draft?.Version, latest.WorkflowVersion, updated.UpdatedAt, updated.UpdatedBy);
            })
            .OrderBy(s => messages.Pick(s.Name), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<BuilderServiceDto> GetAsync(Caller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var list = await versions.ListForCodeAsync(code, ct);
        if (list.Count == 0) throw new NotFoundException("errors.service.notFound");
        return ToDto(code, list);
    }

    /// <summary>Starts a new service as a draft (version 1). It is not visible to citizens until published.</summary>
    public async Task<BuilderServiceDto> CreateAsync(Caller caller, ServiceDefinition definition, CancellationToken ct)
    {
        EnsureStaff(caller);
        if (string.IsNullOrWhiteSpace(definition.Code)) throw new ValidationException([new FieldError("code", messages.Format("builder.rule.code"))]);
        if ((await versions.ListForCodeAsync(definition.Code, ct)).Count > 0)
            throw new ValidationException([new FieldError("code", messages.Format("builder.rule.codeExists"))]);

        await versions.AddAsync(ServiceVersion.NewDraft(definition, 1, caller.Name), ct);
        await versions.SaveChangesAsync(ct);
        return await GetAsync(caller, definition.Code, ct);
    }

    /// <summary>Saves the draft, starting the next version from the published one if there is no draft yet.</summary>
    public async Task<BuilderServiceDto> SaveDraftAsync(Caller caller, string code, ServiceDefinition definition,
        CancellationToken ct)
    {
        EnsureStaff(caller);
        if (!string.Equals(definition.Code, code, StringComparison.Ordinal))
            throw new DomainException("errors.service.codeChange");

        var list = await versions.ListForCodeAsync(code, ct);
        if (list.Count == 0) throw new NotFoundException("errors.service.notFound");

        var draft = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft);
        if (draft is null)
            await versions.AddAsync(ServiceVersion.NewDraft(definition, list.Max(v => v.Version) + 1, caller.Name), ct);
        else
            draft.UpdateDraft(definition, caller.Name);

        await versions.SaveChangesAsync(ct);
        return await GetAsync(caller, code, ct);
    }

    /// <summary>Publishes the draft as the live version; the previous live version is retired.</summary>
    public async Task<BuilderServiceDto> PublishAsync(Caller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var list = await versions.ListForCodeAsync(code, ct);
        var draft = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft)
                    ?? throw new DomainException("errors.service.noDraftToPublish");

        var previous = list.Where(v => v.Status == ServiceVersionStatus.Published).ToList();
        var definition = draft.ToDefinition();
        var currencyErrors = ServiceDefinitionRules.Validate(definition, messages, await CurrencyOfAsync(definition, ct))
            .Where(e => e.Field is "currency" or "fee").ToList();
        if (currencyErrors.Count > 0) throw new ValidationException(currencyErrors);
        draft.Publish(caller.Name, messages); // throws ValidationException while the definition has errors
        foreach (var live in previous) live.Retire();
        await versions.SaveChangesAsync(ct);
        return await GetAsync(caller, code, ct);
    }

    /// <summary>Throws the draft away. A service that was never published is removed entirely.</summary>
    public async Task<BuilderServiceDto?> DiscardDraftAsync(Caller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var list = await versions.ListForCodeAsync(code, ct);
        var draft = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft)
                    ?? throw new DomainException("errors.service.noDraftToDiscard");
        versions.Remove(draft);
        await versions.SaveChangesAsync(ct);
        return list.Count == 1 ? null : await GetAsync(caller, code, ct);
    }

    /// <summary>Checks a definition without saving it, for the builder's live feedback.</summary>
    public async Task<IReadOnlyList<FieldError>> ValidateAsync(Caller caller, ServiceDefinition definition, CancellationToken ct)
    {
        EnsureStaff(caller);
        return ServiceDefinitionRules.Validate(definition, messages, await CurrencyOfAsync(definition, ct));
    }

    /// <summary>The platform's record of the service's currency, or null when the platform doesn't know it.</summary>
    private async Task<CurrencyInfo?> CurrencyOfAsync(ServiceDefinition definition, CancellationToken ct)
    {
        var list = await currencies.ListAsync(ct);
        if (list.Count == 0) return null; // platform unreachable: don't block staff on it
        return list.FirstOrDefault(c => string.Equals(c.Code, definition.Currency, StringComparison.OrdinalIgnoreCase))
               ?? CurrencyInfo.Fallback(definition.Currency) with { IsEnabled = false };
    }

    private BuilderServiceDto ToDto(string code, IReadOnlyList<ServiceVersion> list)
    {
        var published = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Published)?.ToDefinition();
        var draft = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft)?.ToDefinition();
        return new BuilderServiceDto(code, published, draft,
            draft is null ? [] : ServiceDefinitionRules.Validate(draft, messages),
            list.OrderByDescending(v => v.Version).Select(ServiceVersionDto.From).ToList());
    }

    private static void EnsureStaff(Caller caller)
    {
        if (!caller.IsStaff) throw new ForbiddenException("errors.staffOnly");
    }
}

/// <summary>Imports the service definitions shipped with the platform when the catalog is empty.</summary>
public sealed class CatalogSeeder(IServiceVersionRepository versions, ILocalization localization)
{
    public async Task<int> SeedAsync(IEnumerable<ServiceDefinition> definitions, CancellationToken ct)
    {
        if (await versions.AnyAsync(ct)) return 0;
        var messages = await localization.GetMessagesAsync(null, ct);
        var count = 0;
        foreach (var definition in definitions)
        {
            var version = ServiceVersion.NewDraft(definition, 1, "Platform");
            version.Publish("Platform", messages);
            await versions.AddAsync(version, ct);
            count++;
        }
        await versions.SaveChangesAsync(ct);
        return count;
    }
}
