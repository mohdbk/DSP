using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// An e-service as data: its fee, its process and the steps and fields users complete. Every human-facing text is a
/// <see cref="LocalizedText"/> in any number of languages; only the platform's default language is required.
/// Built in the back-office service builder (or produced by the OutSystems extraction pipeline) and published as
/// numbered versions. The form view, chat view, validation and AI review all read this one definition.
/// </summary>
public sealed class ServiceDefinition
{
    public required string Code { get; init; }
    public required LocalizedText Name { get; init; }
    public required LocalizedText Description { get; init; }
    public LocalizedText Category { get; init; } = LocalizedText.Of("en", "General");
    public decimal Fee { get; init; }
    public string Currency { get; init; } = "AED";

    /// <summary>Field whose value identifies the application in lists and uniqueness checks.</summary>
    public string? ReferenceField { get; init; }

    /// <summary>Published version of this definition. Applications keep the version they started on.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The process applications follow: see <see cref="WorkflowVersions"/>.</summary>
    public int WorkflowVersion { get; init; } = WorkflowVersions.Standard;

    /// <summary>What the AI pre-review checks for this service (agentic process only), in plain language.</summary>
    public IReadOnlyList<LocalizedText>? ReviewChecklist { get; init; }

    public required IReadOnlyList<FormStep> Steps { get; init; }

    [JsonIgnore]
    public IEnumerable<FieldDefinition> AllFields => Steps.SelectMany(s => s.Fields);

    public FieldDefinition? FindField(string key) =>
        AllFields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
}

public static class WorkflowVersions
{
    /// <summary>v1, the traditional journey: staff review, payment, certificate.</summary>
    public const int Standard = 1;

    /// <summary>
    /// v2, AI-assisted (partially agentic): an AI review first — it can ask the applicant to fix things and gives the
    /// officer a recommendation and findings — then an officer decides every case.
    /// </summary>
    public const int AiAssisted = 2;

    /// <summary>
    /// v3, agentic: as AI-assisted, and the AI approves clear cases itself (see Applications.AutoApprovalPolicy).
    /// Anything unclear, every rejection and an audit sample still go to an officer.
    /// </summary>
    public const int Agentic = 3;

    public static bool IsKnown(int version) => version is Standard or AiAssisted or Agentic;

    public static readonly IReadOnlyList<int> All = [Standard, AiAssisted, Agentic];

    /// <summary>The AI reviews applications (AI-assisted and agentic).</summary>
    public static bool UsesAi(int version) => version is AiAssisted or Agentic;
}

public sealed class FormStep
{
    public required string Id { get; init; }
    public required LocalizedText Title { get; init; }
    public LocalizedText? Description { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
}

public static class FieldTypes
{
    public const string Text = "text";
    public const string TextArea = "textarea";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Number = "number";
    public const string Date = "date";
    public const string Select = "select";
    public const string File = "file";
    public const string Checkbox = "checkbox";

    public static readonly IReadOnlyList<string> All =
        [Text, TextArea, Email, Phone, Number, Date, Select, File, Checkbox];
}

public sealed class FieldDefinition
{
    public required string Key { get; init; }
    public required LocalizedText Label { get; init; }
    public string Type { get; init; } = FieldTypes.Text;
    public bool Required { get; init; }
    public LocalizedText? Help { get; init; }
    public LocalizedText? Placeholder { get; init; }
    public IReadOnlyList<FieldOption>? Options { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public string? Pattern { get; init; }
    public LocalizedText? PatternMessage { get; init; }

    /// <summary>Profile claim used to pre-fill the field from the signed-in user (e.g. "name", "email").</summary>
    public string? PrefillFrom { get; init; }
}

public sealed class FieldOption
{
    public required string Value { get; init; }
    public required LocalizedText Label { get; init; }
}

/// <summary>Reads and writes definitions as JSON (every text a language map).</summary>
public static class ServiceDefinitionJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serialize(ServiceDefinition definition) => JsonSerializer.Serialize(definition, Json);

    public static ServiceDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize<ServiceDefinition>(json, Json) ?? throw new DomainException("errors.service.unreadable");

    public static ServiceDefinition Deserialize(Stream json) =>
        JsonSerializer.Deserialize<ServiceDefinition>(json, Json) ?? throw new DomainException("errors.service.unreadable");
}
