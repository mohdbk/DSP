using System.Security.Claims;
using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.Documents.Application;
using Dsp.Documents.Domain;
using Dsp.Documents.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-documents");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddDocumentsApplication();
builder.Services.AddDocumentsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync<DocumentsDbContext>();

// Service-to-service: store generated documents (e.g. certificates) on behalf of a user.
app.MapPost("/internal/documents", async (IFormFile file, [FromForm] string ownerId, [FromForm] string? kind,
        DocumentService svc, CancellationToken ct) =>
    {
        var documentKind = Enum.TryParse<DocumentKind>(kind, true, out var k) ? k : DocumentKind.Attachment;
        await using var stream = file.OpenReadStream();
        return Results.Ok(await svc.UploadAsync(ownerId, file.FileName, file.ContentType, stream, documentKind, ct));
    })
    .RequireInternalKey().AllowAnonymous().DisableAntiforgery();

app.MapApiDocuments("documents");

var api = app.MapApi("documents").RequireAuthorization();

api.MapPost("/documents", async (IFormFile file, ClaimsPrincipal user, DocumentService svc, CancellationToken ct) =>
{
    await using var stream = file.OpenReadStream();
    return Results.Ok(await svc.UploadAsync(user.UserId(), file.FileName, file.ContentType, stream,
        DocumentKind.Attachment, ct));
}).DisableAntiforgery();

api.MapGet("/documents/{id:guid}", async (Guid id, ClaimsPrincipal user, DocumentService svc, CancellationToken ct) =>
    await svc.GetAsync(user.UserId(), user.IsStaff(), id, ct));

api.MapGet("/documents/{id:guid}/content", async (Guid id, ClaimsPrincipal user, DocumentService svc,
    CancellationToken ct) =>
{
    var (document, content) = await svc.OpenAsync(user.UserId(), user.IsStaff(), id, ct);
    return Results.File(content, document.ContentType, document.FileName);
});

app.Run();
