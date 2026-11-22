using Dsp.Identity.Domain;

namespace Dsp.Identity.Application;

/// <summary>
/// A pluggable sign-in provider for citizens and companies (redirect-based OAuth/OIDC).
/// UAE PASS is the only one enabled today; Google or others are added by implementing this interface.
/// </summary>
public interface ICitizenIdentityProvider
{
    /// <summary>Route-safe key, e.g. "uaepass".</summary>
    string Name { get; }
    string DisplayName { get; }
    bool IsEnabled { get; }
    string BuildChallengeUrl(string state, string redirectUri);
    Task<ExternalProfile> CompleteAsync(IReadOnlyDictionary<string, string> callback, string redirectUri,
        CancellationToken ct);
    string? BuildLogoutUrl(string redirectUri);
}

/// <summary>
/// Authenticates back-office staff. Local credentials today; Active Directory later,
/// selected by configuration without changing callers.
/// </summary>
public interface IStaffAuthenticator
{
    string Name { get; }
    Task<PlatformUser?> AuthenticateAsync(string username, string password, CancellationToken ct);
}

public interface IStaffAccountRepository
{
    Task<StaffAccount?> FindByUsernameAsync(string username, CancellationToken ct);
    Task<bool> AnyAsync(CancellationToken ct);
    Task AddAsync(StaffAccount account, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

/// <summary>Issues the platform's own access token after authentication.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(PlatformUser user);
}

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record SignInResult(IssuedToken Token, UserDto User);

public sealed record ProviderDto(string Name, string DisplayName);

public sealed record UserDto(
    string Id,
    string FullName,
    string? FullNameAr,
    string? EmiratesId,
    string? Email,
    string? Mobile,
    string? Title,
    string Role,
    string Provider,
    string? AccountLevel)
{
    public static UserDto From(PlatformUser u) => new(u.Subject, u.FullName, u.FullNameAr, u.EmiratesId, u.Email,
        u.Mobile, u.Title, u.Role.ToString().ToLowerInvariant(), u.Provider, u.AccountLevel);
}
