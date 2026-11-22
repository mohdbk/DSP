using System.Security.Claims;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.EServices.Application;
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

await app.EnsureDatabaseAsync<EServicesDbContext>();

static Caller ToCaller(ClaimsPrincipal user) => new(
    user.UserId(),
    user.DisplayName(),
    user.IsStaff(),
    new Dictionary<string, string?>
    {
        ["name"] = user.FindFirstValue(DspClaims.Name),
        ["emirates_id"] = user.FindFirstValue(DspClaims.EmiratesId),
        ["email"] = user.FindFirstValue(DspClaims.Email),
        ["mobile"] = user.FindFirstValue(DspClaims.Mobile),
    }.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => p.Value));

var api = app.MapGroup("/api/eservices").RequireAuthorization();

// Catalog
api.MapGet("/services", (ApplicationService svc) => svc.ListServices()).AllowAnonymous();
api.MapGet("/services/{code}", (string code, ApplicationService svc) => svc.GetService(code)).AllowAnonymous();

// Applications (portal form view, and the AI agent's tools)
api.MapPost("/applications", async (CreateApplicationRequest req, ClaimsPrincipal user, ApplicationService svc,
        CancellationToken ct) =>
    Results.Ok(await svc.CreateDraftAsync(ToCaller(user), req.ServiceCode, ct)));

api.MapGet("/applications", async (bool? all, ClaimsPrincipal user, ApplicationService svc, CancellationToken ct) =>
    await svc.ListAsync(ToCaller(user), all == true, ct));

api.MapGet("/applications/{id:guid}", async (Guid id, ClaimsPrincipal user, ApplicationService svc,
        CancellationToken ct) =>
    await svc.GetAsync(ToCaller(user), id, ct));

api.MapPut("/applications/{id:guid}/data", async (Guid id, SaveDataRequest req, ClaimsPrincipal user,
        ApplicationService svc, CancellationToken ct) =>
    await svc.SaveDataAsync(ToCaller(user), id, req.Values, ct));

api.MapPost("/applications/{id:guid}/validate", async (Guid id, ClaimsPrincipal user, ApplicationService svc,
        CancellationToken ct) =>
    Results.Ok(new { errors = await svc.ValidateAsync(ToCaller(user), id, ct) }));

api.MapPost("/applications/{id:guid}/submit", async (Guid id, ClaimsPrincipal user, ApplicationService svc,
        CancellationToken ct) =>
    await svc.SubmitAsync(ToCaller(user), id, ct));

// Back office
var staff = api.MapGroup("/tasks").RequireAuthorization("staff");
staff.MapGet("", async (ClaimsPrincipal user, ReviewService svc, CancellationToken ct) =>
    await svc.ListOpenAsync(ToCaller(user), ct));
staff.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ReviewService svc, CancellationToken ct) =>
    await svc.GetAsync(ToCaller(user), id, ct));
staff.MapPost("/{id:guid}/decision", async (Guid id, DecisionRequest req, ClaimsPrincipal user, ReviewService svc,
        CancellationToken ct) =>
    await svc.DecideAsync(ToCaller(user), id, req, ct));

app.Run();
