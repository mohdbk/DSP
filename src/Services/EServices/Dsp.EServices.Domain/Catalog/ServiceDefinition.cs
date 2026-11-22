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
    /// v2, agentic: an AI pre-review first (it can ask the applicant for more information); clear cases are approved
    /// automatically when the service allows it, everything else goes to staff.
    /// </summary>
    public const int AiAssisted = 2;

    public static bool IsKnown(int version) => version is Standard or AiAssisted;
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

/// <summary>
/// Reads and writes definitions as JSON. Definitions saved before languages became dynamic kept Arabic in separate
/// properties (<c>name</c> + <c>nameAr</c>); reading folds those into the language maps, so old versions still open.
/// </summary>
public static class ServiceDefinitionJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] LocalizedProperties =
        ["name", "description", "category", "title", "label", "help", "placeholder", "patternMessage"];

    public static string Serialize(ServiceDefinition definition) => JsonSerializer.Serialize(definition, Json);

    public static ServiceDefinition Deserialize(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new DomainException("errors.service.unreadable");
        Upgrade(node);
        return node.Deserialize<ServiceDefinition>(Json) ?? throw new DomainException("errors.service.unreadable");
    }

    public static ServiceDefinition Deserialize(Stream json)
    {
        using var reader = new StreamReader(json);
        return Deserialize(reader.ReadToEnd());
    }

    private static void Upgrade(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in LocalizedProperties)
                {
                    var arName = name + "Ar";
                    if (!obj.TryGetPropertyValue(arName, out var arabic)) continue;
                    obj.Remove(arName);
                    var arabicText = arabic?.GetValueKind() == JsonValueKind.String ? arabic.GetValue<string>() : null;
                    if (obj[name] is JsonObject existing)
                    {
                        if (!string.IsNullOrWhiteSpace(arabicText)) existing["ar"] = arabicText;
                        continue;
                    }
                    var map = obj[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
                        ? new JsonObject { ["en"] = value.GetValue<string>() }
                        : new JsonObject();
                    if (!string.IsNullOrWhiteSpace(arabicText)) map["ar"] = arabicText;
                    obj[name] = map.Count > 0 ? map : null;
                }
                foreach (var (_, child) in obj.ToList())
                    if (child is not null) Upgrade(child);
                break;
            case JsonArray array:
                foreach (var child in array)
                    if (child is not null) Upgrade(child);
                break;
        }
    }
}
