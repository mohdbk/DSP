using System.Security.Claims;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.Payments.Application;
using Dsp.Payments.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-payments");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddPaymentsApplication();
builder.Services.AddPaymentsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync<PaymentsDbContext>();

// Service-to-service: other services create payment requests.
app.MapPost("/internal/payments", async (CreatePaymentRequest req, PaymentService svc, CancellationToken ct) =>
    Results.Ok(await svc.CreateAsync(req, ct))).RequireInternalKey().AllowAnonymous();

// Payer-facing.
var api = app.MapGroup("/api/payments").RequireAuthorization();
api.MapGet("/payments", async (Guid? referenceId, ClaimsPrincipal user, PaymentService svc, CancellationToken ct) =>
    await svc.ListAsync(user.UserId(), referenceId, ct));
api.MapGet("/payments/{id:guid}", async (Guid id, ClaimsPrincipal user, PaymentService svc, CancellationToken ct) =>
    await svc.GetAsync(user.UserId(), user.IsStaff(), id, ct));
api.MapPost("/payments/{id:guid}/pay", async (Guid id, ClaimsPrincipal user, PaymentService svc, CancellationToken ct) =>
    await svc.PayAsync(user.UserId(), id, ct));

app.Run();
