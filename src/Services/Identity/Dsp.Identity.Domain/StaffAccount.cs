using Dsp.SharedKernel;

namespace Dsp.Identity.Domain;

/// <summary>
/// A back-office account for local (username and password) sign-in.
/// Replaced by the ministry directory (AD) later; the platform user it produces stays the same.
/// </summary>
public sealed class StaffAccount : Entity
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private StaffAccount() { }

    public string Username { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public bool IsActive { get; private set; } = true;
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSignInAt { get; private set; }

    public static StaffAccount Create(string username, string fullName, string title, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new DomainException("Username is required.");
        return new StaffAccount
        {
            Username = username.Trim().ToLowerInvariant(),
            FullName = fullName,
            Title = title,
            Email = email,
            PasswordHash = passwordHash,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public void RecordFailedAttempt(DateTimeOffset now)
    {
        FailedAttempts++;
        if (FailedAttempts >= MaxFailedAttempts)
        {
            LockedUntil = now.Add(LockoutDuration);
            FailedAttempts = 0;
        }
    }

    public void RecordSignIn(DateTimeOffset now)
    {
        FailedAttempts = 0;
        LockedUntil = null;
        LastSignInAt = now;
    }

    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public PlatformUser ToPlatformUser() => new()
    {
        Subject = $"staff:{Username}",
        FullName = FullName,
        Email = Email,
        Title = Title,
        Role = PlatformRole.Staff,
        Provider = "local",
    };
}
