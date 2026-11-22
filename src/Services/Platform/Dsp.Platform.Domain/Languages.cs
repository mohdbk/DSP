using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.Platform.Domain;

public static class TextDirections
{
    public const string LeftToRight = "ltr";
    public const string RightToLeft = "rtl";
    public static bool IsKnown(string? value) => value is LeftToRight or RightToLeft;
}

/// <summary>
/// A language the platform can show. Languages are data: they are added, renamed, enabled, disabled and deleted at
/// runtime. Exactly one is the default; it is always enabled, every text falls back to it, and only it is required.
/// </summary>
public sealed partial class Language : Entity
{
    private Language() { }

    /// <summary>BCP 47 code, e.g. "en", "ar", "ur", "zh-Hans".</summary>
    public string Code { get; private set; } = "";

    /// <summary>The language's name in any number of languages (e.g. { en: "Arabic", ar: "العربية" }).</summary>
    public LocalizedText Name { get; private set; } = new();

    public string Direction { get; private set; } = TextDirections.LeftToRight;
    public bool IsEnabled { get; private set; }
    public bool IsDefault { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";

    public static Language Create(string code, LocalizedText name, string direction, int sortOrder, string by)
    {
        var normalized = (code ?? "").Trim();
        if (!CodePattern().IsMatch(normalized)) throw new DomainException("errors.language.code");
        var language = new Language { Code = normalized, IsEnabled = false };
        language.Update(name, direction, sortOrder, by);
        return language;
    }

    public void Update(LocalizedText name, string direction, int sortOrder, string by)
    {
        if (name.IsEmpty) throw new DomainException("errors.language.name");
        if (!TextDirections.IsKnown(direction)) throw new DomainException("errors.language.direction");
        Name = new LocalizedText(name);
        Direction = direction;
        SortOrder = sortOrder;
        Touch(by);
    }

    public void Enable(string by)
    {
        IsEnabled = true;
        Touch(by);
    }

    public void Disable(string by)
    {
        if (IsDefault) throw new DomainException("errors.language.disableDefault");
        IsEnabled = false;
        Touch(by);
    }

    /// <summary>Makes this the default (and enables it). The caller clears the previous default.</summary>
    internal void MakeDefault(string by)
    {
        IsDefault = true;
        IsEnabled = true;
        Touch(by);
    }

    internal void ClearDefault(string by)
    {
        IsDefault = false;
        Touch(by);
    }

    private void Touch(string by)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = by;
    }

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex CodePattern();
}

/// <summary>The rules that span languages: one default at all times, and the default can't be deleted.</summary>
public static class LanguagePolicy
{
    public static void SetDefault(IEnumerable<Language> all, Language chosen, string by)
    {
        foreach (var language in all.Where(l => l.IsDefault && l != chosen)) language.ClearDefault(by);
        chosen.MakeDefault(by);
    }

    public static void EnsureCanDelete(Language language)
    {
        if (language.IsDefault) throw new DomainException("errors.language.deleteDefault");
    }
}

/// <summary>The text of one message key in one language. A key exists when the default language has a text for it.</summary>
public sealed partial class Translation : Entity
{
    private Translation() { }

    public string Key { get; private set; } = "";
    public string LanguageCode { get; private set; } = "";
    public string Text { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";

    public static Translation Create(string key, string languageCode, string text, string by)
    {
        if (!KeyPattern().IsMatch(key ?? "")) throw new DomainException("errors.translation.key");
        var translation = new Translation { Key = key!, LanguageCode = languageCode };
        translation.Change(text, by);
        return translation;
    }

    public void Change(string text, string by)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("errors.translation.empty");
        if (text.Length > 4000) throw new DomainException("errors.translation.tooLong");
        Text = text;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = by;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,200}$")]
    private static partial Regex KeyPattern();
}
