using System.Text.Json;
using Dsp.SharedKernel;

namespace Dsp.Notifications.Domain;

/// <summary>
/// An in-app notification. Title and body are message references (a key plus arguments), rendered in each reader's
/// language when read, so they follow language changes and languages added later. Email, SMS and push attach here.
/// </summary>
public sealed class Notification : Entity
{
    /// <summary>Recipient used for messages meant for every back-office user.</summary>
    public const string StaffAudience = "role:staff";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private Notification() { }

    public string Recipient { get; private set; } = "";

    /// <summary>The title as a message reference (JSON).</summary>
    public string TitleMessage { get; private set; } = "";

    /// <summary>The body as a message reference (JSON).</summary>
    public string BodyMessage { get; private set; } = "";

    public string? Link { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(string recipient, MessageRef title, MessageRef body, string? link) => new()
    {
        Recipient = recipient,
        TitleMessage = JsonSerializer.Serialize(title, Json),
        BodyMessage = JsonSerializer.Serialize(body, Json),
        Link = link,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>The title in the reader's language.</summary>
    public string RenderTitle(IMessages messages) =>
        Render(TitleMessage, messages);

    /// <summary>The body in the reader's language.</summary>
    public string RenderBody(IMessages messages) =>
        Render(BodyMessage, messages);

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;

    private static string Render(string json, IMessages messages) =>
        JsonSerializer.Deserialize<MessageRef>(json, Json)?.Render(messages) ?? "";
}
