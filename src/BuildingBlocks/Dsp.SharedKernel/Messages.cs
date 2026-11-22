namespace Dsp.SharedKernel;

/// <summary>
/// Human-facing messages by key, in one language (e.g. "validation.required" → "{field} is required.").
/// The texts are data in the Localization service; domain and application code only ever use keys and arguments.
/// </summary>
public interface IMessages
{
    /// <summary>The language these messages are in.</summary>
    string Language { get; }

    /// <summary>The platform default language, used when a text has no translation.</summary>
    string DefaultLanguage { get; }

    /// <summary>The message for <paramref name="key"/> with its {placeholders} filled; the key itself if it has no text.</summary>
    string Format(string key, IReadOnlyDictionary<string, string>? args = null);
}

public static class MessagesExtensions
{
    public static string Format(this IMessages messages, string key, params (string Name, object? Value)[] args) =>
        messages.Format(key, args.ToDictionary(a => a.Name, a => Convert.ToString(a.Value, System.Globalization.CultureInfo.InvariantCulture) ?? ""));

    /// <summary>Picks a language map's text in these messages' language (falling back to the default).</summary>
    public static string Pick(this IMessages messages, LocalizedText? text) =>
        text?.Get(messages.Language, messages.DefaultLanguage) ?? "";
}

/// <summary>A message to show later, in whatever language the reader uses: a key plus its arguments.</summary>
/// <param name="Args">Argument values; a value may itself be in several languages.</param>
public sealed record MessageRef(string Key, IReadOnlyDictionary<string, LocalizedText>? Args = null)
{
    public static MessageRef Of(string key, params (string Name, object? Value)[] args) =>
        new(key, args.ToDictionary(a => a.Name, a => a.Value switch
        {
            LocalizedText text => text,
            null => new LocalizedText(),
            var other => LocalizedText.Of("*", Convert.ToString(other, System.Globalization.CultureInfo.InvariantCulture) ?? ""),
        }));

    /// <summary>The message in the given messages' language.</summary>
    public string Render(IMessages messages) =>
        messages.Format(Key, Args?.ToDictionary(a => a.Key, a => a.Value.Get(messages.Language, messages.DefaultLanguage)));
}

/// <summary>Formats "{name}" placeholders.</summary>
public static class MessageTemplate
{
    public static string Fill(string template, IReadOnlyDictionary<string, string>? args)
    {
        if (args is null || args.Count == 0) return template;
        var result = template;
        foreach (var (name, value) in args) result = result.Replace("{" + name + "}", value, StringComparison.Ordinal);
        return result;
    }
}
