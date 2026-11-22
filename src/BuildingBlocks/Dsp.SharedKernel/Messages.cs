namespace Dsp.SharedKernel;

/// <summary>
/// Human-facing messages by key, in one language (e.g. "validation.required" → "{field} is required.").
/// The texts are data in the Platform service; domain and application code only ever use keys and arguments.
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
[System.Text.Json.Serialization.JsonConverter(typeof(MessageRefConverter))]
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

/// <summary>Reads a message reference; a plain string (data written before messages were keys) becomes a key that renders as itself.</summary>
public sealed class MessageRefConverter : System.Text.Json.Serialization.JsonConverter<MessageRef>
{
    public override MessageRef? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null) return null;

        string key = "";
        Dictionary<string, LocalizedText>? args = null;
        while (reader.Read() && reader.TokenType != System.Text.Json.JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, "key", StringComparison.OrdinalIgnoreCase)) key = reader.GetString() ?? "";
            else if (string.Equals(name, "args", StringComparison.OrdinalIgnoreCase) &&
                     reader.TokenType != System.Text.Json.JsonTokenType.Null)
                args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, LocalizedText>>(ref reader, options);
            else reader.Skip();
        }
        return new MessageRef(key, args);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, MessageRef value,
        System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("key", value.Key);
        if (value.Args is { Count: > 0 })
        {
            writer.WritePropertyName("args");
            System.Text.Json.JsonSerializer.Serialize(writer, value.Args, options);
        }
        writer.WriteEndObject();
    }
}
