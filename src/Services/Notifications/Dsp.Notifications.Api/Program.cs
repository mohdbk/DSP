using Dsp.SharedKernel;
using System.Security.Claims;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.Notifications.Application;
using Dsp.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-notifications");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync<NotificationsDbContext>();

app.MapApiDocuments("notifications");

var api = app.MapApi("notifications").RequireAuthorization();
api.MapGet("/me", async (int? page, int? pageSize, ClaimsPrincipal user, NotificationService svc, CancellationToken ct) =>
    PagedList.From(await svc.ListAsync(user.UserId(), user.IsStaff(), ct), page, pageSize));
api.MapPost("/{id:guid}/read", async (Guid id, ClaimsPrincipal user, NotificationService svc, CancellationToken ct) =>
{
    await svc.MarkReadAsync(user.UserId(), user.IsStaff(), id, ct);
    return Results.NoContent();
});

app.Run();
