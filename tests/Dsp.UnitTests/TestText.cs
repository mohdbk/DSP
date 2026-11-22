using System.Text.Json;
using Dsp.SharedKernel;

namespace Dsp.UnitTests;

/// <summary>
/// The shipped message catalogue (the Platform seed file), so tests see the same texts as the running system:
/// English is the default language and Arabic is enabled.
/// </summary>
public static class TestText
{
    private static readonly Lazy<Dictionary<string, LocalizedText>> Strings = new(Load);

    public static IMessages Messages(string language = "en") =>
        new CatalogMessages(language, "en", Strings.Value.Where(s => s.Value.Has(language) || s.Value.Has("en"))
            .ToDictionary(s => s.Key, s => s.Value.Get(language, "en")));

    public static ILocalization Localization { get; } = new SeedLocalization();

    public static LocalizedText Text(string en, string? ar = null) =>
        ar is null ? LocalizedText.Of("en", en) : new LocalizedText([new("en", en), new("ar", ar)]);

    private static Dictionary<string, LocalizedText> Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dsp.sln"))) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "src", "Services", "Platform", "Dsp.Platform.Infrastructure", "Seed", "translations.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("strings").Deserialize<Dictionary<string, LocalizedText>>()!;
    }

    private sealed class SeedLocalization : ILocalization
    {
        private static readonly LanguageSettings Settings = new([
            new LanguageInfo("en", Text("English", "الإنجليزية"), "ltr", true, true, 0),
            new LanguageInfo("ar", Text("Arabic", "العربية"), "rtl", true, false, 1),
        ]);

        public Task<LanguageSettings> GetSettingsAsync(CancellationToken ct) => Task.FromResult(Settings);

        public Task<IReadOnlyDictionary<string, string>> GetCatalogAsync(string language, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(Strings.Value.ToDictionary(s => s.Key, s => s.Value.Get(language, "en")));
    }
}
