using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// Validates application data against a service definition. The same rules run for
/// the form view, the chat view and final submission, so they exist only once.
/// </summary>
public static class FormValidator
{
    /// <param name="onlyKeys">Validate just these fields (partial save). Null validates every field.</param>
    public static List<FieldError> Validate(ServiceDefinition definition, IReadOnlyDictionary<string, string?> data,
        IEnumerable<string>? onlyKeys = null)
    {
        var errors = new List<FieldError>();
        var keys = onlyKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.AllFields)
        {
            if (keys is not null && !keys.Contains(field.Key)) continue;
            data.TryGetValue(field.Key, out var raw);
            var error = ValidateField(field, raw?.Trim());
            if (error is not null) errors.Add(new FieldError(field.Key, error));
        }

        if (keys is not null)
        {
            foreach (var unknown in keys.Where(k => definition.FindField(k) is null))
                errors.Add(new FieldError(unknown, "This field is not part of the service."));
        }

        return errors;
    }

    public static string? ValidateField(FieldDefinition field, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (field.Type == FieldTypes.Checkbox && field.Required) return $"Please confirm: {field.Label}.";
            return field.Required ? $"{field.Label} is required." : null;
        }

        switch (field.Type)
        {
            case FieldTypes.Email:
                if (!MailAddress.TryCreate(value, out _)) return "Enter a valid email address.";
                break;
            case FieldTypes.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return $"{field.Label} must be a number.";
                if (field.Min is { } min && number < min) return $"{field.Label} must be at least {min}.";
                if (field.Max is { } max && number > max) return $"{field.Label} must be at most {max}.";
                break;
            case FieldTypes.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{field.Label} must be a date (YYYY-MM-DD).";
                break;
            case FieldTypes.Select:
                if (field.Options is { Count: > 0 } options && options.All(o => o.Value != value))
                    return $"Choose a valid option for {field.Label}.";
                break;
            case FieldTypes.File:
                if (!Guid.TryParse(value, out _)) return $"Upload a file for {field.Label}.";
                break;
            case FieldTypes.Checkbox:
                if (field.Required && value != "true") return $"Please confirm: {field.Label}.";
                break;
        }

        if (field.MinLength is { } minLength && value.Length < minLength)
            return $"{field.Label} must be at least {minLength} characters.";
        if (field.MaxLength is { } maxLength && value.Length > maxLength)
            return $"{field.Label} must be at most {maxLength} characters.";
        if (!string.IsNullOrEmpty(field.Pattern) &&
            !Regex.IsMatch(value, field.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)))
            return field.PatternMessage ?? $"{field.Label} is not in the right format.";

        return null;
    }
}
