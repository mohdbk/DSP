using System.Text.Json.Serialization;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// An e-service as data: its fee, its process and the steps and fields users complete, in English and Arabic.
/// Built in the back-office service builder (or produced by the OutSystems extraction pipeline) and
/// published as numbered versions. The form view, chat view, validation and AI review all read this one definition.
/// </summary>
public sealed class ServiceDefinition
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? NameAr { get; init; }
    public required string Description { get; init; }
    public string? DescriptionAr { get; init; }
    public string Category { get; init; } = "General";
    public string? CategoryAr { get; init; }
    public decimal Fee { get; init; }
    public string Currency { get; init; } = "AED";

    /// <summary>Field whose value identifies the application in lists and uniqueness checks.</summary>
    public string? ReferenceField { get; init; }

    /// <summary>Published version of this definition. Applications keep the version they started on.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The process applications follow: see <see cref="WorkflowVersions"/>.</summary>
    public int WorkflowVersion { get; init; } = WorkflowVersions.Standard;

    /// <summary>What the AI pre-review checks for this service (agentic process only), in plain language.</summary>
    public IReadOnlyList<string>? ReviewChecklist { get; init; }

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

    /// <summary>v2, agentic: an AI pre-review first (it can ask the applicant for more information), then staff decide.</summary>
    public const int AiAssisted = 2;

    public static bool IsKnown(int version) => version is Standard or AiAssisted;
}

public sealed class FormStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleAr { get; init; }
    public string? Description { get; init; }
    public string? DescriptionAr { get; init; }
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
    public required string Label { get; init; }
    public string? LabelAr { get; init; }
    public string Type { get; init; } = FieldTypes.Text;
    public bool Required { get; init; }
    public string? Help { get; init; }
    public string? HelpAr { get; init; }
    public string? Placeholder { get; init; }
    public string? PlaceholderAr { get; init; }
    public IReadOnlyList<FieldOption>? Options { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public string? Pattern { get; init; }
    public string? PatternMessage { get; init; }
    public string? PatternMessageAr { get; init; }

    /// <summary>Profile claim used to pre-fill the field from the signed-in user (e.g. "name", "email").</summary>
    public string? PrefillFrom { get; init; }

    public string LabelFor(string lang) => Localized.Pick(lang, Label, LabelAr);
}

public sealed class FieldOption
{
    public required string Value { get; init; }
    public required string Label { get; init; }
    public string? LabelAr { get; init; }
}

/// <summary>A text in English and Arabic.</summary>
public sealed record LocalizedText(string En, string Ar)
{
    public string For(string lang) => Localized.Pick(lang, En, Ar);
}

public static class Localized
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>Normalises an Accept-Language style value to "en" or "ar".</summary>
    public static string Normalize(string? lang) =>
        lang is not null && lang.TrimStart().StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? Arabic : English;

    /// <summary>The Arabic text when asked for Arabic and one exists, otherwise the English text.</summary>
    public static string Pick(string? lang, string en, string? ar) =>
        Normalize(lang) == Arabic && !string.IsNullOrWhiteSpace(ar) ? ar : en;
}
