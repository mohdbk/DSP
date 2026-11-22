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
    public string? TitleAr { get; private set; }
    public string? BodyAr { get; private set; }
    public string? Link { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(string recipient, string title, string body, string? link,
        string? titleAr = null, string? bodyAr = null) => new()
    {
        Recipient = recipient,
        Title = Cut(title, 300),
        Body = Cut(body, 2000),
        TitleAr = titleAr is null ? null : Cut(titleAr, 300),
        BodyAr = bodyAr is null ? null : Cut(bodyAr, 2000),
        Link = link,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
}
