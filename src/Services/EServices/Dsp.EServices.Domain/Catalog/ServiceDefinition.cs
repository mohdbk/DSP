namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// An e-service as data: its fee and the steps and fields users complete.
/// This is the shape the OutSystems extraction pipeline produces per service.
/// The form view, chat view and validation all read this one definition.
/// </summary>
public sealed class ServiceDefinition
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? NameAr { get; init; }
    public required string Description { get; init; }
    public string Category { get; init; } = "General";
    public decimal Fee { get; init; }
    public string Currency { get; init; } = "AED";

    /// <summary>Field whose value identifies the application in lists and uniqueness checks.</summary>
    public string? ReferenceField { get; init; }

    public int WorkflowVersion { get; init; } = 1;
    public required IReadOnlyList<FormStep> Steps { get; init; }

    public IEnumerable<FieldDefinition> AllFields => Steps.SelectMany(s => s.Fields);

    public FieldDefinition? FindField(string key) =>
        AllFields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
}

public sealed class FormStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
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
}

public sealed class FieldDefinition
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public string Type { get; init; } = FieldTypes.Text;
    public bool Required { get; init; }
    public string? Help { get; init; }
    public string? Placeholder { get; init; }
    public IReadOnlyList<FieldOption>? Options { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public string? Pattern { get; init; }
    public string? PatternMessage { get; init; }

    /// <summary>Profile claim used to pre-fill the field from the signed-in user (e.g. "name", "email").</summary>
    public string? PrefillFrom { get; init; }
}

public sealed class FieldOption
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}
