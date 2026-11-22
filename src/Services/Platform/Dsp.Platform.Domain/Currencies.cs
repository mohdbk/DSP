using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.Platform.Domain;

public static class SymbolPositions
{
    public const string Before = "before";
    public const string After = "after";
    public static bool IsKnown(string? value) => value is Before or After;
}

/// <summary>
/// A currency services can charge in: ISO 4217 code, its name in any number of languages, how amounts are written
/// (symbol, where it goes, how many decimals) and whether services may use it. Exactly one is the default.
/// </summary>
public sealed partial class Currency : Entity
{
    public const int MaxDecimals = 4;

    private Currency() { }

    /// <summary>ISO 4217 code, e.g. "AED", "USD", "KWD".</summary>
    public string Code { get; private set; } = "";
    public LocalizedText Name { get; private set; } = new();

    /// <summary>What is written next to amounts, e.g. "AED" or "$". UAE content rules use "AED".</summary>
    public string Symbol { get; private set; } = "";
    public string SymbolPosition { get; private set; } = SymbolPositions.Before;

    /// <summary>Minor-unit digits: 2 for AED, 0 for JPY, 3 for KWD, BHD and OMR.</summary>
    public int Decimals { get; private set; } = 2;

    public bool IsEnabled { get; private set; }
    public bool IsDefault { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";

    public static Currency Create(string code, LocalizedText name, string symbol, string symbolPosition, int decimals,
        int sortOrder, string by)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized)) throw new ValidationException([new FieldError("code", "errors.currency.code")]);
        var currency = new Currency { Code = normalized };
        currency.Update(name, symbol, symbolPosition, decimals, sortOrder, by);
        return currency;
    }

    public void Update(LocalizedText name, string symbol, string symbolPosition, int decimals, int sortOrder, string by)
    {
        var errors = new List<FieldError>();
        if (name is null || name.IsEmpty) errors.Add(new FieldError("name", "errors.currency.name"));
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Trim().Length > 10) errors.Add(new FieldError("symbol", "errors.currency.symbol"));
        if (!SymbolPositions.IsKnown(symbolPosition)) errors.Add(new FieldError("symbolPosition", "errors.currency.position"));
        if (decimals is < 0 or > MaxDecimals) errors.Add(new FieldError("decimals", "errors.currency.decimals"));
        if (errors.Count > 0) throw new ValidationException(errors);

        Name = new LocalizedText(name!);
        Symbol = symbol.Trim();
        SymbolPosition = symbolPosition;
        Decimals = decimals;
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
        if (IsDefault) throw new DomainException("errors.currency.disableDefault");
        IsEnabled = false;
        Touch(by);
    }

    /// <summary>Makes this the default (and enables it); clears the previous default.</summary>
    public static void SetDefault(IEnumerable<Currency> all, Currency chosen, string by)
    {
        foreach (var c in all.Where(c => c.IsDefault && c != chosen)) { c.IsDefault = false; c.Touch(by); }
        chosen.IsDefault = true;
        chosen.IsEnabled = true;
        chosen.Touch(by);
    }

    public void EnsureCanDelete()
    {
        if (IsDefault) throw new DomainException("errors.currency.deleteDefault");
    }

    private void Touch(string by)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = by;
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CodePattern();
}
