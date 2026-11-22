using System.Security.Cryptography;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.Identity.Application;
using Dsp.Identity.Infrastructure;
using Dsp.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-identity");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddDataProtection();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync<IdentityDbContext>();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<StaffAccountSeeder>()
        .SeedAsync(app.Configuration["Staff:InitialPassword"], CancellationToken.None);
}

const string StateCookie = "dsp_signin_state";
var publicBaseUrl = (app.Configuration["Identity:PublicBaseUrl"] ?? "").TrimEnd('/');

string BaseUrl(HttpContext ctx) =>
    string.IsNullOrEmpty(publicBaseUrl) ? $"{ctx.Request.Scheme}://{ctx.Request.Host}" : publicBaseUrl;

string CallbackUri(HttpContext ctx, string provider) =>
    // Registered with UAE PASS; the gateway forwards this unversioned path to the current version.
    $"{BaseUrl(ctx)}/api/identity/citizen/{provider}/callback";

static string SafeReturnUrl(string? returnUrl) =>
    !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";

app.MapApiDocuments("identity");

var api = app.MapApi("identity");

// Citizens: pluggable external providers (UAE PASS now).
api.MapGet("/providers", (CitizenSignInService svc) => svc.ListProviders()).AllowAnonymous();

api.MapGet("/citizen/{provider}/login", (string provider, string? returnUrl, HttpContext ctx,
    CitizenSignInService svc, IDataProtectionProvider dp) =>
{
    var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var protector = dp.CreateProtector("dsp.signin.state");
    ctx.Response.Cookies.Append(StateCookie, protector.Protect($"{state}|{SafeReturnUrl(returnUrl)}"),
        new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api",
            MaxAge = TimeSpan.FromMinutes(10),
        });
    return Results.Redirect(svc.Start(provider, state, CallbackUri(ctx, provider)));
}).AllowAnonymous();

api.MapGet("/citizen/{provider}/callback", async (string provider, HttpContext ctx, CitizenSignInService svc,
    IDataProtectionProvider dp, ILogger<Program> logger, CancellationToken ct) =>
{
    var baseUrl = BaseUrl(ctx);
    var query = ctx.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
    var cookie = ctx.Request.Cookies[StateCookie];
    ctx.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/api" });

    string expectedState, returnUrl;
    try
    {
        var parts = dp.CreateProtector("dsp.signin.state").Unprotect(cookie ?? "").Split('|', 2);
        (expectedState, returnUrl) = (parts[0], parts[1]);
    }
    catch (Exception)
    {
        return Results.Redirect($"{baseUrl}/login?error={Uri.EscapeDataString("errors.signIn.expired")}");
    }

    if (!query.TryGetValue("state", out var state) || state != expectedState)
        return Results.Redirect($"{baseUrl}/login?error={Uri.EscapeDataString("errors.signIn.unverified")}");

    try
    {
        var result = await svc.CompleteAsync(provider, query, CallbackUri(ctx, provider), ct);
        // Token goes in the fragment so it never reaches server logs.
        return Results.Redirect($"{baseUrl}/auth/callback#token={Uri.EscapeDataString(result.Token.AccessToken)}" +
                                $"&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
    catch (Exception ex) when (ex is Dsp.SharedKernel.DomainException or Dsp.SharedKernel.NotFoundException)
    {
        logger.LogInformation("Citizen sign-in failed: {Message}", ex.Message);
        // The portal shows the message key in the reader's language.
        return Results.Redirect($"{baseUrl}/login?error={Uri.EscapeDataString(ex.Message)}");
    }
}).AllowAnonymous();

api.MapGet("/citizen/{provider}/logout", (string provider, HttpContext ctx, CitizenSignInService svc) =>
{
    var url = svc.LogoutUrl(provider, $"{BaseUrl(ctx)}/login");
    return Results.Redirect(url ?? $"{BaseUrl(ctx)}/login");
}).AllowAnonymous();

// Back office: local credentials now, AD later.
api.MapPost("/staff/login", async (StaffLoginRequest req, StaffSignInService svc, CancellationToken ct) =>
{
    var result = await svc.SignInAsync(req.Username, req.Password, ct);
    return Results.Ok(new { accessToken = result.Token.AccessToken, expiresAt = result.Token.ExpiresAt, user = result.User });
}).AllowAnonymous();

api.MapGet("/me", (HttpContext ctx) => Results.Ok(new
{
    id = ctx.User.UserId(),
    fullName = ctx.User.DisplayName(),
    role = ctx.User.FindFirst(DspClaims.Role)?.Value,
    emiratesId = ctx.User.FindFirst(DspClaims.EmiratesId)?.Value,
    email = ctx.User.FindFirst(DspClaims.Email)?.Value,
    mobile = ctx.User.FindFirst(DspClaims.Mobile)?.Value,
    title = ctx.User.FindFirst("title")?.Value,
    provider = ctx.User.FindFirst("idp")?.Value,
    accountLevel = ctx.User.FindFirst("account_level")?.Value,
})).RequireAuthorization();

app.Run();

public sealed record StaffLoginRequest(string Username, string Password);
