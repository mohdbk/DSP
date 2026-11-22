using System.Security.Claims;
using Dsp.BuildingBlocks.Localization;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.Platform.Application;
using Dsp.Platform.Infrastructure;
using Dsp.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-platform");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddPlatformApplication();
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddDspLocalization(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync<PlatformDbContext>();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<PlatformSeeder>().SeedAsync(ShippedTexts.Load(), CancellationToken.None);

app.UseDspLocalization();
app.MapApiDocuments("platform");

static StaffCaller Caller(ClaimsPrincipal user) => new(user.UserId(), user.DisplayName(), user.IsStaff());

var api = app.MapApi("platform");

// Public: what the portals need before anyone signs in.
api.MapGet("/languages", (LanguageService svc, CancellationToken ct) => svc.ListEnabledAsync(ct)).AllowAnonymous();
api.MapGet("/catalog/{lang}", (string lang, TranslationService svc, CancellationToken ct) => svc.GetCatalogAsync(lang, ct))
    .AllowAnonymous();
api.MapGet("/currencies", (CurrencyService svc, CancellationToken ct) => svc.ListAllAsync(ct)).AllowAnonymous();

// Anyone signed in (staff or citizen): their own saved views of the lists.
var views = api.MapGroup("/views").RequireAuthorization();
views.MapGet("", (string list, ClaimsPrincipal user, SavedViewService svc, CancellationToken ct) => svc.ListAsync(Caller(user), list, ct));
views.MapPost("", (SavedViewRequest req, ClaimsPrincipal user, SavedViewService svc, CancellationToken ct) =>
    svc.SaveAsync(Caller(user), req, ct));
views.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, SavedViewService svc, CancellationToken ct) =>
{
    await svc.DeleteAsync(Caller(user), id, ct);
    return Results.NoContent();
});

// Staff: languages, texts and AI settings.
var admin = api.MapGroup("/admin").RequireAuthorization("staff");

admin.MapGet("/languages", ([AsParameters] ListQuery query, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
    svc.ListForAdminAsync(Caller(user), query, ct));
admin.MapPost("/languages", (LanguageRequest req, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
    svc.CreateAsync(Caller(user), req, ct));
admin.MapPut("/languages/{code}", (string code, LanguageRequest req, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
    svc.UpdateAsync(Caller(user), code, req, ct));
admin.MapPost("/languages/{code}/enable", (string code, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
    svc.SetEnabledAsync(Caller(user), code, true, ct));
admin.MapPost("/languages/{code}/disable", (string code, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
    svc.SetEnabledAsync(Caller(user), code, false, ct));
admin.MapPost("/languages/{code}/default", (string code, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
    svc.SetDefaultAsync(Caller(user), code, ct));
admin.MapDelete("/languages/{code}", async (string code, ClaimsPrincipal user, LanguageService svc, CancellationToken ct) =>
{
    await svc.DeleteAsync(Caller(user), code, ct);
    return Results.NoContent();
});

admin.MapGet("/translations/{code}", (string code, [AsParameters] ListQuery query, ClaimsPrincipal user,
    TranslationService svc, CancellationToken ct) => svc.ListAsync(Caller(user), code, query, ct));
admin.MapPut("/translations/{code}/{key}", async (string code, string key, SetTranslationRequest req, ClaimsPrincipal user,
    TranslationService svc, CancellationToken ct) =>
{
    await svc.SetAsync(Caller(user), code, key, req.Text, ct);
    return Results.NoContent();
});
admin.MapDelete("/translations/keys/{key}", async (string key, ClaimsPrincipal user, TranslationService svc, CancellationToken ct) =>
{
    await svc.DeleteKeyAsync(Caller(user), key, ct);
    return Results.NoContent();
});
admin.MapGet("/translations/{code}/export", (string code, ClaimsPrincipal user, TranslationService svc, CancellationToken ct) =>
    svc.ExportAsync(Caller(user), code, ct));
admin.MapPost("/translations/{code}/import", (string code, Dictionary<string, string> strings, ClaimsPrincipal user,
    TranslationService svc, CancellationToken ct) => svc.ImportAsync(Caller(user), code, strings, ct));

admin.MapGet("/currencies", ([AsParameters] ListQuery query, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
    svc.ListForAdminAsync(Caller(user), query, ct));
admin.MapPost("/currencies", (CurrencyRequest req, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
    svc.CreateAsync(Caller(user), req, ct));
admin.MapPut("/currencies/{code}", (string code, CurrencyRequest req, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
    svc.UpdateAsync(Caller(user), code, req, ct));
admin.MapPost("/currencies/{code}/enable", (string code, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
    svc.SetEnabledAsync(Caller(user), code, true, ct));
admin.MapPost("/currencies/{code}/disable", (string code, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
    svc.SetEnabledAsync(Caller(user), code, false, ct));
admin.MapPost("/currencies/{code}/default", (string code, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
    svc.SetDefaultAsync(Caller(user), code, ct));
admin.MapDelete("/currencies/{code}", async (string code, ClaimsPrincipal user, CurrencyService svc, CancellationToken ct) =>
{
    await svc.DeleteAsync(Caller(user), code, ct);
    return Results.NoContent();
});

admin.MapGet("/ai", (ClaimsPrincipal user, AiSettingsService svc, CancellationToken ct) => svc.GetAsync(Caller(user), ct));
admin.MapPut("/ai", (AiSettingsRequest req, ClaimsPrincipal user, AiSettingsService svc, CancellationToken ct) =>
    svc.UpdateAsync(Caller(user), req, ct));
admin.MapPost("/ai/test", (ClaimsPrincipal user, AiSettingsService svc, CancellationToken ct) => svc.TestAsync(Caller(user), ct));

// Internal: other services read languages, texts and the AI settings in force (with the key).
var internalApi = app.MapGroup("/internal/platform").RequireInternalKey().AllowAnonymous();
internalApi.MapGet("/languages", (LanguageService svc, CancellationToken ct) => svc.ListAllAsync(ct));
internalApi.MapGet("/catalog/{lang}", async (string lang, TranslationService svc, CancellationToken ct) =>
    (await svc.GetCatalogAsync(lang, ct)).Strings);
internalApi.MapGet("/ai", (AiSettingsService svc, CancellationToken ct) => svc.GetRuntimeAsync(ct));
internalApi.MapGet("/currencies", (CurrencyService svc, CancellationToken ct) => svc.ListAllAsync(ct));
// The AI policy alone (no model address or key), for services that act on the AI's results.
internalApi.MapGet("/ai/policy", (AiSettingsService svc, CancellationToken ct) => svc.GetPolicyAsync(ct));

app.Run();
