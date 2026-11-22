using Dsp.SharedKernel;

namespace Dsp.Notifications.Domain;

/// <summary>An in-app notification. Email, SMS and push channels attach here later.</summary>
public sealed class Notification : Entity
{
    /// <summary>Recipient used for messages meant for every back-office user.</summary>
    public const string StaffAudience = "role:staff";

    private Notification() { }

    public string Recipient { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Body { get; private set; } = "";
    public string? Link { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(string recipient, string title, string body, string? link) => new()
    {
        Recipient = recipient,
        Title = title,
        Body = body,
        Link = link,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
}
