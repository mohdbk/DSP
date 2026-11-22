using System.Globalization;

namespace Dsp.SharedKernel;

/// <summary>A currency as the Platform service defines it: how its amounts are written and rounded.</summary>
public sealed record CurrencyInfo(string Code, LocalizedText Name, string Symbol, string SymbolPosition, int Decimals,
    bool IsEnabled, bool IsDefault, int SortOrder)
{
    /// <summary>Used when Platform doesn't know a code (or can't be reached): the code itself, two decimals.</summary>
    public static CurrencyInfo Fallback(string code) =>
        new(code, LocalizedText.Of("en", code), code, "before", 2, true, false, 0);

    /// <summary>Rounds to the currency's minor unit (banker's rounding is avoided: half away from zero).</summary>
    public decimal Round(decimal amount) => Math.Round(amount, Decimals, MidpointRounding.AwayFromZero);

    /// <summary>True when <paramref name="amount"/> has no more decimals than the currency allows.</summary>
    public bool Fits(decimal amount) => Round(amount) == amount;

    /// <summary>
    /// "AED 1,500" or "AED 1,500.50" (UAE content rules: symbol, space, comma thousands, Latin digits). Whole amounts
    /// drop the decimals; others show all of the currency's decimals.
    /// </summary>
    public string Format(decimal amount)
    {
        var rounded = Round(amount);
        var digits = rounded == decimal.Truncate(rounded) ? 0 : Decimals;
        var number = rounded.ToString("N" + digits, CultureInfo.InvariantCulture);
        return SymbolPosition == "after" ? $"{number} {Symbol}" : $"{Symbol} {number}";
    }
}

/// <summary>The currencies services may use, from the Platform service.</summary>
public interface ICurrencies
{
    Task<IReadOnlyList<CurrencyInfo>> ListAsync(CancellationToken ct);
}

public static class CurrencyExtensions
{
    public static async Task<CurrencyInfo> GetAsync(this ICurrencies currencies, string code, CancellationToken ct) =>
        (await currencies.ListAsync(ct)).FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? CurrencyInfo.Fallback(code);

    public static async Task<string> FormatAsync(this ICurrencies currencies, decimal amount, string code, CancellationToken ct) =>
        (await currencies.GetAsync(code, ct)).Format(amount);
}
