using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.Identity.Application;

/// <summary>Citizen sign-in through whichever external providers are enabled.</summary>
public sealed class CitizenSignInService(
    IEnumerable<ICitizenIdentityProvider> providers,
    ITokenIssuer tokens,
    ILogger<CitizenSignInService> logger)
{
    public IReadOnlyList<ProviderDto> ListProviders() =>
        providers.Where(p => p.IsEnabled).Select(p => new ProviderDto(p.Name, p.DisplayName)).ToList();

    public string Start(string provider, string state, string redirectUri) =>
        Get(provider).BuildChallengeUrl(state, redirectUri);

    public async Task<SignInResult> CompleteAsync(string provider, IReadOnlyDictionary<string, string> callback,
        string redirectUri, CancellationToken ct)
    {
        var profile = await Get(provider).CompleteAsync(callback, redirectUri, ct);
        var user = profile.ToCitizen();
        logger.LogInformation("Citizen sign-in via {Provider} for {Subject} ({Level})", provider, user.Subject,
            user.AccountLevel);
        return new SignInResult(tokens.Issue(user), UserDto.From(user));
    }

    public string? LogoutUrl(string provider, string redirectUri) => Get(provider).BuildLogoutUrl(redirectUri);

    private ICitizenIdentityProvider Get(string name) =>
        providers.FirstOrDefault(p => p.IsEnabled && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotFoundException($"Sign-in provider '{name}' is not available.");
}

/// <summary>Back-office sign-in through the configured staff authenticator.</summary>
public sealed class StaffSignInService(
    IStaffAuthenticator authenticator,
    ITokenIssuer tokens,
    ILogger<StaffSignInService> logger)
{
    public async Task<SignInResult> SignInAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new ValidationException([new FieldError("username", "Enter your username and password.")]);

        var user = await authenticator.AuthenticateAsync(username, password, ct);
        if (user is null)
        {
            logger.LogWarning("Failed staff sign-in for {Username} via {Authenticator}", username, authenticator.Name);
            throw new ForbiddenException("Incorrect username or password.");
        }

        logger.LogInformation("Staff sign-in for {Subject} via {Authenticator}", user.Subject, authenticator.Name);
        return new SignInResult(tokens.Issue(user), UserDto.From(user));
    }
}
