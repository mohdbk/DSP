using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>
/// The no-code service builder: staff create and edit e-services as data, preview them and publish
/// numbered versions. Publishing affects new applications only; those in flight keep their version.
/// </summary>
public sealed class ServiceBuilderService(IServiceVersionRepository versions)
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
                return new BuilderServiceSummaryDto(g.Key, latest.Name, latest.NameAr, latest.Category,
                    published?.Version, draft?.Version, latest.WorkflowVersion, updated.UpdatedAt, updated.UpdatedBy);
            })
            .OrderBy(s => s.Name)
            .ToList();
    }

    public async Task<BuilderServiceDto> GetAsync(Caller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var list = await versions.ListForCodeAsync(code, ct);
        if (list.Count == 0) throw new NotFoundException($"Service '{code}' was not found.");
        return ToDto(code, list);
    }

    /// <summary>Starts a new service as a draft (version 1). It is not visible to citizens until published.</summary>
    public async Task<BuilderServiceDto> CreateAsync(Caller caller, ServiceDefinition definition, CancellationToken ct)
    {
        EnsureStaff(caller);
        if (string.IsNullOrWhiteSpace(definition.Code)) throw new ValidationException([new FieldError("code", "Enter a service code.")]);
        if ((await versions.ListForCodeAsync(definition.Code, ct)).Count > 0)
            throw new ValidationException([new FieldError("code", "A service with this code already exists.")]);

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
            throw new DomainException("The service code can't be changed. Create a new service instead.");

        var list = await versions.ListForCodeAsync(code, ct);
        if (list.Count == 0) throw new NotFoundException($"Service '{code}' was not found.");

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
                    ?? throw new DomainException("There is no draft to publish. Make a change first.");

        draft.Publish(caller.Name); // throws ValidationException while the definition has errors
        foreach (var live in list.Where(v => v.Status == ServiceVersionStatus.Published)) live.Retire();
        await versions.SaveChangesAsync(ct);
        return await GetAsync(caller, code, ct);
    }

    /// <summary>Throws the draft away. A service that was never published is removed entirely.</summary>
    public async Task<BuilderServiceDto?> DiscardDraftAsync(Caller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var list = await versions.ListForCodeAsync(code, ct);
        var draft = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft)
                    ?? throw new DomainException("There is no draft to discard.");
        versions.Remove(draft);
        await versions.SaveChangesAsync(ct);
        return list.Count == 1 ? null : await GetAsync(caller, code, ct);
    }

    /// <summary>Checks a definition without saving it, for the builder's live feedback.</summary>
    public IReadOnlyList<FieldError> Validate(Caller caller, ServiceDefinition definition)
    {
        EnsureStaff(caller);
        return ServiceDefinitionRules.Validate(definition);
    }

    private static BuilderServiceDto ToDto(string code, IReadOnlyList<ServiceVersion> list)
    {
        var published = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Published)?.ToDefinition();
        var draft = list.FirstOrDefault(v => v.Status == ServiceVersionStatus.Draft)?.ToDefinition();
        return new BuilderServiceDto(code, published, draft,
            draft is null ? [] : ServiceDefinitionRules.Validate(draft),
            list.OrderByDescending(v => v.Version).Select(ServiceVersionDto.From).ToList());
    }

    private static void EnsureStaff(Caller caller)
    {
        if (!caller.IsStaff) throw new ForbiddenException("Back-office access only.");
    }
}

/// <summary>Imports the service definitions shipped with the platform when the catalog is empty.</summary>
public sealed class CatalogSeeder(IServiceVersionRepository versions)
{
    public async Task<int> SeedAsync(IEnumerable<ServiceDefinition> definitions, CancellationToken ct)
    {
        if (await versions.AnyAsync(ct)) return 0;
        var count = 0;
        foreach (var definition in definitions)
        {
            var version = ServiceVersion.NewDraft(definition, 1, "Platform");
            version.Publish("Platform");
            await versions.AddAsync(version, ct);
            count++;
        }
        await versions.SaveChangesAsync(ct);
        return count;
    }
}
