namespace Dsp.SharedKernel;

/// <summary>A language the platform offers. Its name is itself multilingual (e.g. Arabic is "Arabic" and "العربية").</summary>
public sealed record LanguageInfo(string Code, LocalizedText Name, string Direction, bool IsEnabled, bool IsDefault, int SortOrder);

/// <summary>The platform's languages: exactly one default, any number of others, each enabled or not.</summary>
public sealed record LanguageSettings(IReadOnlyList<LanguageInfo> Languages)
{
    public string DefaultLanguage => Languages.FirstOrDefault(l => l.IsDefault)?.Code ?? "en";

    public IEnumerable<LanguageInfo> Enabled => Languages.Where(l => l.IsEnabled).OrderBy(l => l.SortOrder);

    /// <summary>Picks the best enabled language for an Accept-Language header; the default when none matches.</summary>
    public string Resolve(string? acceptLanguage)
    {
        var enabled = Enabled.Select(l => l.Code).ToList();
        foreach (var candidate in (acceptLanguage ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(p => p.Split(';')[0].Trim()).Where(p => p.Length > 0))
        {
            var exact = enabled.FirstOrDefault(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;
            var neutral = enabled.FirstOrDefault(c => string.Equals(c, candidate.Split('-')[0], StringComparison.OrdinalIgnoreCase));
            if (neutral is not null) return neutral;
        }
        return DefaultLanguage;
    }
}

/// <summary>Languages and message texts, from the Platform service.</summary>
public interface ILocalization
{
    Task<LanguageSettings> GetSettingsAsync(CancellationToken ct);

    /// <summary>Every message key with its text in <paramref name="language"/>, falling back to the default language.</summary>
    Task<IReadOnlyDictionary<string, string>> GetCatalogAsync(string language, CancellationToken ct);
}

public static class LocalizationExtensions
{
    public static async Task<IMessages> GetMessagesAsync(this ILocalization localization, string? language, CancellationToken ct)
    {
        var settings = await localization.GetSettingsAsync(ct);
        var lang = language is null ? settings.DefaultLanguage : settings.Resolve(language);
        return new CatalogMessages(lang, settings.DefaultLanguage, await localization.GetCatalogAsync(lang, ct));
    }

    /// <summary>Messages in every enabled language, e.g. to store a text all readers can see in their own language.</summary>
    public static async Task<LocalizedText> RenderAllAsync(this ILocalization localization, MessageRef message, CancellationToken ct)
    {
        var settings = await localization.GetSettingsAsync(ct);
        var text = new LocalizedText();
        foreach (var language in settings.Enabled)
            text[language.Code] = message.Render(await localization.GetMessagesAsync(language.Code, ct));
        return text;
    }
}

/// <summary>Messages from a catalogue; a key without a text is shown as is.</summary>
public sealed class CatalogMessages(string language, string defaultLanguage, IReadOnlyDictionary<string, string> catalog) : IMessages
{
    public string Language { get; } = language;
    public string DefaultLanguage { get; } = defaultLanguage;

    public string Format(string key, IReadOnlyDictionary<string, string>? args = null) =>
        MessageTemplate.Fill(catalog.TryGetValue(key, out var text) ? text : key, args);
}

public sealed class EmptyMessages : IMessages
{
    public static readonly EmptyMessages Instance = new();
    public string Language => "en";
    public string DefaultLanguage => "en";
    public string Format(string key, IReadOnlyDictionary<string, string>? args = null) => MessageTemplate.Fill(key, args);
}

