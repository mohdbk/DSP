using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dsp.SharedKernel;

/// <summary>
/// A text in any number of languages, keyed by language code: <c>{ "en": "Fee", "ar": "الرسوم", "ur": "فیس" }</c>.
/// Languages are data (managed in the Localization service); only the default language is required and
/// every reader falls back to it. Used for all human-facing content: service definitions, names, messages.
/// </summary>
[JsonConverter(typeof(LocalizedTextConverter))]
public sealed class LocalizedText : Dictionary<string, string>
{
    public LocalizedText() : base(StringComparer.OrdinalIgnoreCase) { }

    public LocalizedText(IEnumerable<KeyValuePair<string, string>> values) : this()
    {
        foreach (var (lang, text) in values)
            if (!string.IsNullOrWhiteSpace(lang) && !string.IsNullOrWhiteSpace(text)) this[lang] = text;
    }

    public static LocalizedText Of(string language, string text) => new() { [language] = text };

    /// <summary>The text in <paramref name="language"/>, else in <paramref name="fallback"/>, else any text there is.</summary>
    public string Get(string? language, string? fallback = null)
    {
        if (language is not null && TryGetValue(language, out var exact) && !string.IsNullOrWhiteSpace(exact)) return exact;
        if (language is not null && language.Contains('-') && TryGetValue(language.Split('-')[0], out var neutral) &&
            !string.IsNullOrWhiteSpace(neutral)) return neutral;
        if (fallback is not null && TryGetValue(fallback, out var dflt) && !string.IsNullOrWhiteSpace(dflt)) return dflt;
        return Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
    }

    public bool Has(string language) => TryGetValue(language, out var v) && !string.IsNullOrWhiteSpace(v);

    public bool IsEmpty => Values.All(string.IsNullOrWhiteSpace);
}

/// <summary>Reads a language map; a plain string (older data) becomes the text of the "en" key.</summary>
public sealed class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String) return LocalizedText.Of("en", reader.GetString() ?? "");
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected a language map.");

        var text = new LocalizedText();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var lang = reader.GetString()!;
            reader.Read();
            var value = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            if (!string.IsNullOrWhiteSpace(value)) text[lang] = value;
        }
        return text;
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (lang, text) in value) writer.WriteString(lang, text);
        writer.WriteEndObject();
    }
}
