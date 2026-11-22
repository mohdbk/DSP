using System.Security.Claims;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Catalog;
using Dsp.EServices.Infrastructure;
using Dsp.EServices.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-eservices");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddEServicesApplication();
builder.Services.AddEServicesInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync<EServicesDbContext>();
await app.Services.SeedServiceCatalogAsync();

static Caller ToCaller(ClaimsPrincipal user, HttpContext http) => new(
    user.UserId(),
    user.DisplayName(),
    user.IsStaff(),
    new Dictionary<string, string?>
    {
        ["name"] = user.FindFirstValue(DspClaims.Name),
        ["emirates_id"] = user.FindFirstValue(DspClaims.EmiratesId),
        ["email"] = user.FindFirstValue(DspClaims.Email),
        ["mobile"] = user.FindFirstValue(DspClaims.Mobile),
    }.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => p.Value),
    Localized.Normalize(http.Request.Headers.AcceptLanguage.ToString()));

var api = app.MapGroup("/api/eservices").RequireAuthorization();

// Catalog (published services; ?version= shows the version an application was filled in on)
api.MapGet("/services", async (ApplicationService svc, CancellationToken ct) => await svc.ListServicesAsync(ct))
    .AllowAnonymous();
api.MapGet("/services/{code}", async (string code, int? version, ApplicationService svc, CancellationToken ct) =>
    await svc.GetServiceAsync(code, version, ct)).AllowAnonymous();

// Applications (portal form view, and the AI agent's tools)
api.MapPost("/applications", async (CreateApplicationRequest req, ClaimsPrincipal user, HttpContext http,
        ApplicationService svc, CancellationToken ct) =>
    Results.Ok(await svc.CreateDraftAsync(ToCaller(user, http), req.ServiceCode, ct)));

api.MapGet("/applications", async (bool? all, ClaimsPrincipal user, HttpContext http, ApplicationService svc,
        CancellationToken ct) =>
    await svc.ListAsync(ToCaller(user, http), all == true, ct));

api.MapGet("/applications/{id:guid}", async (Guid id, ClaimsPrincipal user, HttpContext http, ApplicationService svc,
        CancellationToken ct) =>
    await svc.GetAsync(ToCaller(user, http), id, ct));

api.MapPut("/applications/{id:guid}/data", async (Guid id, SaveDataRequest req, ClaimsPrincipal user, HttpContext http,
        ApplicationService svc, CancellationToken ct) =>
    await svc.SaveDataAsync(ToCaller(user, http), id, req.Values, ct));

api.MapPost("/applications/{id:guid}/validate", async (Guid id, ClaimsPrincipal user, HttpContext http,
        ApplicationService svc, CancellationToken ct) =>
    Results.Ok(new { errors = await svc.ValidateAsync(ToCaller(user, http), id, ct) }));

api.MapPost("/applications/{id:guid}/submit", async (Guid id, ClaimsPrincipal user, HttpContext http,
        ApplicationService svc, CancellationToken ct) =>
    await svc.SubmitAsync(ToCaller(user, http), id, ct));

api.MapGet("/applications/{id:guid}/assessments", async (Guid id, ClaimsPrincipal user, HttpContext http,
        ApplicationService svc, CancellationToken ct) =>
    await svc.ListAssessmentsAsync(ToCaller(user, http), id, ct)).RequireAuthorization("staff");

// Back office: review inbox
var staff = api.MapGroup("/tasks").RequireAuthorization("staff");
staff.MapGet("", async (ClaimsPrincipal user, HttpContext http, ReviewService svc, CancellationToken ct) =>
    await svc.ListOpenAsync(ToCaller(user, http), ct));
staff.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, HttpContext http, ReviewService svc, CancellationToken ct) =>
    await svc.GetAsync(ToCaller(user, http), id, ct));
staff.MapPost("/{id:guid}/decision", async (Guid id, DecisionRequest req, ClaimsPrincipal user, HttpContext http,
        ReviewService svc, CancellationToken ct) =>
    await svc.DecideAsync(ToCaller(user, http), id, req, ct));

// Back office: service builder
var builderApi = api.MapGroup("/builder").RequireAuthorization("staff");
builderApi.MapGet("/services", async (ClaimsPrincipal user, HttpContext http, ServiceBuilderService svc,
        CancellationToken ct) =>
    await svc.ListAsync(ToCaller(user, http), ct));
builderApi.MapGet("/services/{code}", async (string code, ClaimsPrincipal user, HttpContext http,
        ServiceBuilderService svc, CancellationToken ct) =>
    await svc.GetAsync(ToCaller(user, http), code, ct));
builderApi.MapPost("/services", async (ServiceDefinition definition, ClaimsPrincipal user, HttpContext http,
        ServiceBuilderService svc, CancellationToken ct) =>
    await svc.CreateAsync(ToCaller(user, http), definition, ct));
builderApi.MapPut("/services/{code}/draft", async (string code, ServiceDefinition definition, ClaimsPrincipal user,
        HttpContext http, ServiceBuilderService svc, CancellationToken ct) =>
    await svc.SaveDraftAsync(ToCaller(user, http), code, definition, ct));
builderApi.MapDelete("/services/{code}/draft", async (string code, ClaimsPrincipal user, HttpContext http,
        ServiceBuilderService svc, CancellationToken ct) =>
    await svc.DiscardDraftAsync(ToCaller(user, http), code, ct) is { } remaining ? Results.Ok(remaining) : Results.NoContent());
builderApi.MapPost("/services/{code}/publish", async (string code, ClaimsPrincipal user, HttpContext http,
        ServiceBuilderService svc, CancellationToken ct) =>
    await svc.PublishAsync(ToCaller(user, http), code, ct));
builderApi.MapPost("/validate", (ServiceDefinition definition, ClaimsPrincipal user, HttpContext http,
        ServiceBuilderService svc) =>
    Results.Ok(new { errors = svc.Validate(ToCaller(user, http), definition) }));

app.Run();
