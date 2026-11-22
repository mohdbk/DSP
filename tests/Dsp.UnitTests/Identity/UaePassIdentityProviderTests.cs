using System.Net;
using System.Text;
using Dsp.Identity.Infrastructure.Providers;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsp.UnitTests.Identity;

public class UaePassIdentityProviderTests
{
    private const string Redirect = "https://portal.example/api/identity/citizen/uaepass/callback";

    private static readonly UaePassOptions Options = new()
    {
        BaseUrl = "https://stg-id.uaepass.ae/idshub",
        ClientId = "sandbox_stage",
        ClientSecret = "sandbox_stage",
    };

    private static (UaePassIdentityProvider Provider, StubHandler Handler) Create(string userInfoJson)
    {
        var handler = new StubHandler(userInfoJson);
        return (new UaePassIdentityProvider(new HttpClient(handler), Options, NullLogger<UaePassIdentityProvider>.Instance), handler);
    }

    private const string FullProfile = """
        {"sub":"sub-1","uuid":"uuid-123","fullnameEN":"Fatima Al Mansoori","fullnameAR":"فاطمة المنصوري",
         "idn":"784199012345671","email":"fatima@example.ae","mobile":"971501234567","userType":"SOP3"}
        """;

    [Fact]
    public void Challenge_url_targets_staging_with_the_expected_parameters()
    {
        var (provider, _) = Create(FullProfile);

        var url = provider.BuildChallengeUrl("state-1", Redirect);

        Assert.StartsWith("https://stg-id.uaepass.ae/idshub/authorize?response_type=code", url);
        Assert.Contains("client_id=sandbox_stage", url);
        Assert.Contains("state=state-1", url);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(Redirect)}", url);
        Assert.Contains("acr_values=", url);
    }

    [Fact]
    public async Task Callback_exchanges_the_code_and_maps_the_profile()
    {
        var (provider, handler) = Create(FullProfile);

        var profile = await provider.CompleteAsync(new Dictionary<string, string> { ["code"] = "code-1" }, Redirect, default);

        Assert.Equal("uaepass", profile.Provider);
        Assert.Equal("uuid-123", profile.ExternalId);
        Assert.Equal("Fatima Al Mansoori", profile.FullName);
        Assert.Equal("784-1990-1234567-1", profile.EmiratesId);
        Assert.Equal("SOP3", profile.AccountLevel);
        Assert.Equal("uaepass:uuid-123", profile.ToCitizen().Subject);

        // Client credentials go in Basic auth on the back channel.
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("sandbox_stage:sandbox_stage"));
        Assert.Equal($"Basic {expected}", handler.TokenAuthorization);
        Assert.Equal("Bearer access-1", handler.UserInfoAuthorization);
    }

    [Fact]
    public async Task Name_falls_back_to_first_and_last_name()
    {
        var (provider, _) = Create("""{"sub":"s","firstnameEN":"Ali","lastnameEN":"Hassan"}""");

        var profile = await provider.CompleteAsync(new Dictionary<string, string> { ["code"] = "c" }, Redirect, default);

        Assert.Equal("Ali Hassan", profile.FullName);
        Assert.Null(profile.EmiratesId);
        Assert.Equal("s", profile.ExternalId);
    }

    [Fact]
    public async Task Cancelled_sign_in_is_a_clear_error()
    {
        var (provider, _) = Create(FullProfile);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            provider.CompleteAsync(new Dictionary<string, string> { ["error"] = "access_denied" }, Redirect, default));

        Assert.Contains("cancelled", ex.Message);
    }

    [Fact]
    public async Task Failed_token_exchange_is_a_domain_error()
    {
        var handler = new StubHandler(FullProfile) { TokenStatus = HttpStatusCode.BadRequest };
        var provider = new UaePassIdentityProvider(new HttpClient(handler), Options, NullLogger<UaePassIdentityProvider>.Instance);

        await Assert.ThrowsAsync<DomainException>(() =>
            provider.CompleteAsync(new Dictionary<string, string> { ["code"] = "bad" }, Redirect, default));
    }

    [Fact]
    public void Provider_is_disabled_without_credentials()
    {
        var provider = new UaePassIdentityProvider(new HttpClient(), new UaePassOptions(), NullLogger<UaePassIdentityProvider>.Instance);

        Assert.False(provider.IsEnabled);
    }

    private sealed class StubHandler(string userInfoJson) : HttpMessageHandler
    {
        public HttpStatusCode TokenStatus { get; init; } = HttpStatusCode.OK;
        public string? TokenAuthorization { get; private set; }
        public string? UserInfoAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/token"))
            {
                TokenAuthorization = request.Headers.Authorization?.ToString();
                return Task.FromResult(new HttpResponseMessage(TokenStatus)
                {
                    Content = new StringContent("""{"access_token":"access-1","token_type":"Bearer"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (path.EndsWith("/userinfo"))
            {
                UserInfoAuthorization = request.Headers.Authorization?.ToString();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(userInfoJson, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
