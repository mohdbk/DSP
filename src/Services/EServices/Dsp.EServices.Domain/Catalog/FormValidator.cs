using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// Validates application data against a service definition. The same rules run for
/// the form view, the chat view and final submission, so they exist only once.
/// Messages are returned in the caller's language (English or Arabic).
/// </summary>
public static class FormValidator
{
    /// <param name="onlyKeys">Validate just these fields (partial save). Null validates every field.</param>
    /// <param name="lang">"en" or "ar".</param>
    public static List<FieldError> Validate(ServiceDefinition definition, IReadOnlyDictionary<string, string?> data,
        IEnumerable<string>? onlyKeys = null, string lang = Localized.English)
    {
        var errors = new List<FieldError>();
        var keys = onlyKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.AllFields)
        {
            if (keys is not null && !keys.Contains(field.Key)) continue;
            data.TryGetValue(field.Key, out var raw);
            var error = ValidateField(field, raw?.Trim(), lang);
            if (error is not null) errors.Add(new FieldError(field.Key, error));
        }

        if (keys is not null)
        {
            foreach (var unknown in keys.Where(k => definition.FindField(k) is null))
                errors.Add(new FieldError(unknown, T(lang, "This field is not part of the service.", "هذا الحقل ليس جزءًا من الخدمة.")));
        }

        return errors;
    }

    public static string? ValidateField(FieldDefinition field, string? value, string lang = Localized.English)
    {
        var label = field.LabelFor(lang);
        if (string.IsNullOrEmpty(value))
        {
            if (field.Type == FieldTypes.Checkbox && field.Required)
                return T(lang, $"Please confirm: {label}.", $"يرجى التأكيد: {label}.");
            return field.Required ? T(lang, $"{label} is required.", $"{label} مطلوب.") : null;
        }

        switch (field.Type)
        {
            case FieldTypes.Email:
                if (!MailAddress.TryCreate(value, out _))
                    return T(lang, "Enter a valid email address.", "أدخل بريدًا إلكترونيًا صحيحًا.");
                break;
            case FieldTypes.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return T(lang, $"{label} must be a number.", $"يجب أن يكون {label} رقمًا.");
                if (field.Min is { } min && number < min)
                    return T(lang, $"{label} must be at least {min}.", $"يجب ألا يقل {label} عن {min}.");
                if (field.Max is { } max && number > max)
                    return T(lang, $"{label} must be at most {max}.", $"يجب ألا يزيد {label} عن {max}.");
                break;
            case FieldTypes.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return T(lang, $"{label} must be a date (YYYY-MM-DD).", $"يجب أن يكون {label} تاريخًا (YYYY-MM-DD).");
                break;
            case FieldTypes.Select:
                if (field.Options is { Count: > 0 } options && options.All(o => o.Value != value))
                    return T(lang, $"Choose a valid option for {label}.", $"اختر خيارًا صحيحًا لـ {label}.");
                break;
            case FieldTypes.File:
                if (!Guid.TryParse(value, out _)) return T(lang, $"Upload a file for {label}.", $"ارفع ملفًا لـ {label}.");
                break;
            case FieldTypes.Checkbox:
                if (field.Required && value != "true") return T(lang, $"Please confirm: {label}.", $"يرجى التأكيد: {label}.");
                break;
        }

        if (field.MinLength is { } minLength && value.Length < minLength)
            return T(lang, $"{label} must be at least {minLength} characters.", $"يجب ألا يقل {label} عن {minLength} أحرف.");
        if (field.MaxLength is { } maxLength && value.Length > maxLength)
            return T(lang, $"{label} must be at most {maxLength} characters.", $"يجب ألا يزيد {label} عن {maxLength} حرفًا.");
        if (!string.IsNullOrEmpty(field.Pattern) &&
            !Regex.IsMatch(value, field.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)))
        {
            return Localized.Normalize(lang) == Localized.Arabic
                ? field.PatternMessageAr ?? $"صيغة {label} غير صحيحة."
                : field.PatternMessage ?? $"{label} is not in the right format.";
        }

        return null;
    }

    private static string T(string lang, string en, string ar) => Localized.Pick(lang, en, ar);
}
