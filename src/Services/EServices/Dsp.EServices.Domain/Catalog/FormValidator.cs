using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// Validates application data against a service definition. The same rules run for the form view, the chat view
/// and final submission, so they exist only once. Messages come back in the language of <see cref="IMessages"/>.
/// </summary>
public static class FormValidator
{
    /// <param name="onlyKeys">Validate just these fields (partial save). Null validates every field.</param>
    public static List<FieldError> Validate(ServiceDefinition definition, IReadOnlyDictionary<string, string?> data,
        IMessages messages, IEnumerable<string>? onlyKeys = null)
    {
        var errors = new List<FieldError>();
        var keys = onlyKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.AllFields)
        {
            if (keys is not null && !keys.Contains(field.Key)) continue;
            data.TryGetValue(field.Key, out var raw);
            var error = ValidateField(field, raw?.Trim(), messages);
            if (error is not null) errors.Add(new FieldError(field.Key, error));
        }

        if (keys is not null)
        {
            foreach (var unknown in keys.Where(k => definition.FindField(k) is null))
                errors.Add(new FieldError(unknown, messages.Format("validation.unknownField")));
        }

        return errors;
    }

    public static string? ValidateField(FieldDefinition field, string? value, IMessages messages)
    {
        var label = messages.Pick(field.Label);
        string M(string key, params (string, object?)[] args) =>
            messages.Format(key, [("label", label), .. args]);

        if (string.IsNullOrEmpty(value))
        {
            if (field.Type == FieldTypes.Checkbox && field.Required) return M("validation.confirm");
            return field.Required ? M("validation.required") : null;
        }

        switch (field.Type)
        {
            case FieldTypes.Email:
                if (!MailAddress.TryCreate(value, out _)) return M("validation.email");
                break;
            case FieldTypes.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return M("validation.number");
                if (field.Min is { } min && number < min) return M("validation.min", ("min", min));
                if (field.Max is { } max && number > max) return M("validation.max", ("max", max));
                break;
            case FieldTypes.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return M("validation.date");
                break;
            case FieldTypes.Select:
                if (field.Options is { Count: > 0 } options && options.All(o => o.Value != value)) return M("validation.option");
                break;
            case FieldTypes.File:
                if (!Guid.TryParse(value, out _)) return M("validation.file");
                break;
            case FieldTypes.Checkbox:
                if (field.Required && value != "true") return M("validation.confirm");
                break;
        }

        if (field.MinLength is { } minLength && value.Length < minLength) return M("validation.minLength", ("min", minLength));
        if (field.MaxLength is { } maxLength && value.Length > maxLength) return M("validation.maxLength", ("max", maxLength));
        if (!string.IsNullOrEmpty(field.Pattern) &&
            !Regex.IsMatch(value, field.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)))
        {
            var custom = messages.Pick(field.PatternMessage);
            return custom.Length > 0 ? custom : M("validation.pattern");
        }

        return null;
    }
}
