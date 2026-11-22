namespace Dsp.EServices.Application;

/// <summary>
/// Finds existing reference values (e.g. trade names) that look like the applicant's, so the AI pre-review
/// and the officer can spot names that are confusingly similar. Exact duplicates are already refused on submit.
/// </summary>
public static class ReferenceSimilarity
{
    public const double Threshold = 0.75;

    public static IReadOnlyList<string> FindSimilar(string? reference, IEnumerable<string> existing, int max = 5)
    {
        if (string.IsNullOrWhiteSpace(reference)) return [];
        var target = Normalize(reference);
        if (target.Length == 0) return [];

        return existing
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(e => (Value: e, Score: Similarity(target, Normalize(e))))
            .Where(x => x.Score >= Threshold)
            .OrderByDescending(x => x.Score)
            .Take(max)
            .Select(x => x.Value)
            .ToList();
    }

    /// <summary>1 for identical, 0 for nothing in common (normalised Levenshtein distance).</summary>
    public static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        var distance = Levenshtein(a, b);
        return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
    }

    private static readonly string[] NoiseWords = ["llc", "l.l.c", "trading", "est", "establishment", "company", "co"];

    private static string Normalize(string value)
    {
        var words = value.ToLowerInvariant()
            .Split([' ', '-', '_', '.', ',', '&'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !NoiseWords.Contains(w));
        return string.Concat(words);
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
