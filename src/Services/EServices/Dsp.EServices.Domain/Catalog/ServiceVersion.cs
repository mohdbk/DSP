using System.Text.Json;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

public enum ServiceVersionStatus
{
    /// <summary>Being edited in the service builder. At most one per service.</summary>
    Draft,

    /// <summary>What citizens see and new applications use. At most one per service.</summary>
    Published,

    /// <summary>Replaced by a newer published version. Applications started on it keep using it.</summary>
    Retired,
}

/// <summary>
/// One numbered version of an e-service definition. Editing never changes a published version:
/// staff edit a draft and publish it as the next version, so applications in flight are unaffected.
/// </summary>
public sealed class ServiceVersion : Entity
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ServiceVersion() { }

    public string Code { get; private set; } = "";
    public int Version { get; private set; }
    public ServiceVersionStatus Status { get; private set; }
    public string Name { get; private set; } = "";
    public string DefinitionJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? PublishedBy { get; private set; }

    public static ServiceVersion NewDraft(ServiceDefinition definition, int version, string by)
    {
        if (version < 1) throw new DomainException("Version numbers start at 1.");
        var now = DateTimeOffset.UtcNow;
        var draft = new ServiceVersion
        {
            Code = definition.Code,
            Version = version,
            Status = ServiceVersionStatus.Draft,
            CreatedAt = now,
            CreatedBy = by,
        };
        draft.Write(definition, by, now);
        return draft;
    }

    public ServiceDefinition ToDefinition()
    {
        var definition = JsonSerializer.Deserialize<ServiceDefinition>(DefinitionJson, Json)
                         ?? throw new DomainException($"Service {Code} v{Version} has an unreadable definition.");
        definition.Version = Version;
        return definition;
    }

    public void UpdateDraft(ServiceDefinition definition, string by)
    {
        if (Status != ServiceVersionStatus.Draft) throw new DomainException("Only a draft can be edited.");
        if (!string.Equals(definition.Code, Code, StringComparison.Ordinal))
            throw new DomainException("The service code can't be changed. Create a new service instead.");
        Write(definition, by, DateTimeOffset.UtcNow);
    }

    /// <summary>Publishes the draft. Refused while the definition has errors.</summary>
    public void Publish(string by)
    {
        if (Status != ServiceVersionStatus.Draft) throw new DomainException("Only a draft can be published.");
        var errors = ServiceDefinitionRules.Validate(ToDefinition());
        if (errors.Count > 0) throw new ValidationException(errors);
        Status = ServiceVersionStatus.Published;
        PublishedAt = UpdatedAt = DateTimeOffset.UtcNow;
        PublishedBy = UpdatedBy = by;
    }

    public void Retire()
    {
        if (Status != ServiceVersionStatus.Published) throw new DomainException("Only the published version can be retired.");
        Status = ServiceVersionStatus.Retired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Write(ServiceDefinition definition, string by, DateTimeOffset at)
    {
        definition.Version = Version;
        Name = definition.Name ?? "";
        DefinitionJson = JsonSerializer.Serialize(definition, Json);
        UpdatedAt = at;
        UpdatedBy = by;
    }
}
