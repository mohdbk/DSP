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
        if (version < 1) throw new DomainException("errors.service.version");
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
        var definition = ServiceDefinitionJson.Deserialize(DefinitionJson);
        definition.Version = Version;
        return definition;
    }

    public void UpdateDraft(ServiceDefinition definition, string by)
    {
        if (Status != ServiceVersionStatus.Draft) throw new DomainException("errors.service.editDraftOnly");
        if (!string.Equals(definition.Code, Code, StringComparison.Ordinal))
            throw new DomainException("errors.service.codeChange");
        Write(definition, by, DateTimeOffset.UtcNow);
    }

    /// <summary>Publishes the draft. Refused while the definition has errors (reported in the messages' language).</summary>
    public void Publish(string by, IMessages messages)
    {
        if (Status != ServiceVersionStatus.Draft) throw new DomainException("errors.service.publishDraftOnly");
        var errors = ServiceDefinitionRules.Validate(ToDefinition(), messages);
        if (errors.Count > 0) throw new ValidationException(errors);
        Status = ServiceVersionStatus.Published;
        PublishedAt = UpdatedAt = DateTimeOffset.UtcNow;
        PublishedBy = UpdatedBy = by;
    }

    public void Retire()
    {
        if (Status != ServiceVersionStatus.Published) throw new DomainException("errors.service.retirePublishedOnly");
        Status = ServiceVersionStatus.Retired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Write(ServiceDefinition definition, string by, DateTimeOffset at)
    {
        definition.Version = Version;
        Name = Cut(definition.Name?.Get(null) ?? "", 200);
        DefinitionJson = ServiceDefinitionJson.Serialize(definition);
        UpdatedAt = at;
        UpdatedBy = by;
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
