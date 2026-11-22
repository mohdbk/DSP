using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dsp.Identity.Application;
using Dsp.Identity.Domain;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.Identity.Infrastructure.Providers;

public sealed class UaePassOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Staging: https://stg-id.uaepass.ae/idshub — Production: https://id.uaepass.ae/idshub</summary>
    public string BaseUrl { get; set; } = "https://stg-id.uaepass.ae/idshub";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Scope { get; set; } = "urn:uae:digitalid:profile:general";
    public string AcrValues { get; set; } = "urn:safelayer:tws:policies:authentication:level:low";
    public string UiLocales { get; set; } = "en";
}

/// <summary>UAE PASS (citizens and residents). Staging or production is chosen by UaePass:BaseUrl.</summary>
public sealed class UaePassIdentityProvider(
    HttpClient http,
    UaePassOptions options,
    ILogger<UaePassIdentityProvider> logger) : ICitizenIdentityProvider
{
    public string Name => "uaepass";
    public string DisplayName => "UAE PASS";

    public bool IsEnabled => options.Enabled &&
                             !string.IsNullOrWhiteSpace(options.ClientId) &&
                             !string.IsNullOrWhiteSpace(options.ClientSecret);

    public string BuildChallengeUrl(string state, string redirectUri) =>
        $"{options.BaseUrl}/authorize?response_type=code" +
        $"&client_id={Uri.EscapeDataString(options.ClientId!)}" +
        $"&scope={Uri.EscapeDataString(options.Scope)}" +
        $"&state={Uri.EscapeDataString(state)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&acr_values={Uri.EscapeDataString(options.AcrValues)}" +
        $"&ui_locales={Uri.EscapeDataString(options.UiLocales)}";

    public string? BuildLogoutUrl(string redirectUri) =>
        $"{options.BaseUrl}/logout?redirect_uri={Uri.EscapeDataString(redirectUri)}";

    public async Task<ExternalProfile> CompleteAsync(IReadOnlyDictionary<string, string> callback, string redirectUri,
        CancellationToken ct)
    {
        if (callback.TryGetValue("error", out var error))
        {
            logger.LogInformation("UAE PASS returned {Error}", error);
            throw new DomainException(error == "access_denied"
                ? "You cancelled the UAE PASS sign-in."
                : "UAE PASS sign-in could not be completed.");
        }

        if (!callback.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new DomainException("UAE PASS did not return an authorization code.");

        var accessToken = await ExchangeCodeAsync(code, redirectUri, ct);
        return await GetProfileAsync(accessToken, ct);
    }

    private async Task<string> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        // Back channel only. UAE PASS reads the parameters from the token URL; client credentials go in Basic auth.
        var url = $"{options.BaseUrl}/token?grant_type=authorization_code" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&code={Uri.EscapeDataString(code)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["code"] = code,
            }),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("UAE PASS token exchange failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new DomainException("UAE PASS sign-in could not be completed. Please try again.");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new DomainException("UAE PASS returned no access token.");
    }

    private async Task<ExternalProfile> GetProfileAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl}/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("UAE PASS userinfo failed: {Status}", (int)response.StatusCode);
            throw new DomainException("Could not read your UAE PASS profile.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        string? Get(string name) =>
            root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var externalId = Get("uuid") ?? Get("sub") ?? throw new DomainException("UAE PASS profile has no subject.");
        var fullName = Get("fullnameEN") ?? JoinNames(Get("firstnameEN"), Get("lastnameEN")) ?? "UAE PASS user";

        return new ExternalProfile(
            Provider: Name,
            ExternalId: externalId,
            FullName: fullName,
            FullNameAr: Get("fullnameAR"),
            EmiratesId: FormatEmiratesId(Get("idn")),
            Email: Get("email"),
            Mobile: Get("mobile"),
            AccountLevel: Get("userType"));
    }

    private static string? JoinNames(string? first, string? last) =>
        string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last) ? null : $"{first} {last}".Trim();

    /// <summary>UAE PASS returns the 15-digit ID number; the platform uses 784-YYYY-NNNNNNN-N.</summary>
    private static string? FormatEmiratesId(string? idn)
    {
        if (string.IsNullOrWhiteSpace(idn)) return null;
        var digits = new string(idn.Where(char.IsDigit).ToArray());
        return digits.Length == 15 ? $"{digits[..3]}-{digits[3..7]}-{digits[7..14]}-{digits[14]}" : idn;
    }
}
