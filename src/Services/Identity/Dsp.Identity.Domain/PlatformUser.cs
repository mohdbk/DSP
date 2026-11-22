namespace Dsp.Identity.Domain;

public enum PlatformRole
{
    Citizen,
    Staff,
}

/// <summary>A signed-in person as the platform sees them, regardless of how they authenticated.</summary>
public sealed class PlatformUser
{
    public required string Subject { get; init; }
    public required string FullName { get; init; }
    public string? FullNameAr { get; init; }
    public string? EmiratesId { get; init; }
    public string? Email { get; init; }
    public string? Mobile { get; init; }
    public string? Title { get; init; }
    public required PlatformRole Role { get; init; }

    /// <summary>The provider that authenticated the user, e.g. "uaepass", "google", "local", "ad".</summary>
    public required string Provider { get; init; }

    /// <summary>Assurance level reported by the provider, e.g. UAE PASS SOP1, SOP2 or SOP3.</summary>
    public string? AccountLevel { get; init; }
}

/// <summary>
/// A citizen or company representative as returned by any external identity provider
/// (UAE PASS today; Google or others later). Always maps to the citizen role.
/// </summary>
public sealed record ExternalProfile(
    string Provider,
    string ExternalId,
    string FullName,
    string? FullNameAr = null,
    string? EmiratesId = null,
    string? Email = null,
    string? Mobile = null,
    string? AccountLevel = null)
{
    public PlatformUser ToCitizen() => new()
    {
        Subject = $"{Provider}:{ExternalId}",
        FullName = FullName,
        FullNameAr = FullNameAr,
        EmiratesId = EmiratesId,
        Email = Email,
        Mobile = Mobile,
        Role = PlatformRole.Citizen,
        Provider = Provider,
        AccountLevel = AccountLevel,
    };
}
