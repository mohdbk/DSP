using System.Globalization;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Rules;

/// <summary>
/// Business rules beyond field validation, for one service.
/// Extracted from OutSystems server actions; runs for both form and chat submissions.
/// Services built in the service builder have none; they rely on field validation and the AI pre-review.
/// </summary>
public interface IServiceRules
{
    string ServiceCode { get; }

    /// <param name="lang">"en" or "ar": the language of the returned messages.</param>
    IEnumerable<FieldError> Validate(IReadOnlyDictionary<string, string?> data, string lang = Localized.English);
}

public sealed class TradeNameReservationRules : IServiceRules
{
    private static readonly string[] ReservedWords =
        ["ministry", "government", "federal", "royal", "emirates", "national", "authority"];

    public string ServiceCode => "TRADE_NAME_RESERVATION";

    public IEnumerable<FieldError> Validate(IReadOnlyDictionary<string, string?> data, string lang = Localized.English)
    {
        if (data.TryGetValue("tradeNameEn", out var name) && !string.IsNullOrWhiteSpace(name))
        {
            var reserved = ReservedWords.FirstOrDefault(w =>
                name.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (reserved is not null)
                yield return new FieldError("tradeNameEn", Localized.Pick(lang,
                    $"Trade names cannot include the reserved word \"{reserved}\".",
                    $"لا يمكن أن يتضمن الاسم التجاري الكلمة المحجوزة \"{reserved}\"."));
        }
    }
}

public sealed class CommercialAgencyRules : IServiceRules
{
    public string ServiceCode => "COMMERCIAL_AGENCY_REGISTRATION";

    public IEnumerable<FieldError> Validate(IReadOnlyDictionary<string, string?> data, string lang = Localized.English)
    {
        if (TryDate(data, "agreementStart", out var start) && TryDate(data, "agreementEnd", out var end))
        {
            if (end <= start)
                yield return new FieldError("agreementEnd", Localized.Pick(lang,
                    "The agreement must end after it starts.", "يجب أن ينتهي الاتفاق بعد تاريخ بدايته."));
            else if (end < start.AddYears(1))
                yield return new FieldError("agreementEnd", Localized.Pick(lang,
                    "Commercial agency agreements must run for at least one year.",
                    "يجب ألا تقل مدة اتفاقية الوكالة التجارية عن سنة واحدة."));
        }
    }

    private static bool TryDate(IReadOnlyDictionary<string, string?> data, string key, out DateOnly value)
    {
        value = default;
        return data.TryGetValue(key, out var raw) &&
               DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
