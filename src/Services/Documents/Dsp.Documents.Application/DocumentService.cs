using System.Security.Cryptography;
using Dsp.Documents.Domain;
using Dsp.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Documents.Application;

public interface IDocumentRepository
{
    Task<StoredDocument?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(StoredDocument document, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>S3-compatible object storage (SeaweedFS).</summary>
public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream> GetAsync(string key, CancellationToken ct);
}

public sealed record DocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string Sha256,
    string Kind, DateTimeOffset CreatedAt)
{
    public static DocumentDto From(StoredDocument d) =>
        new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Sha256, d.Kind.ToString(), d.CreatedAt);
}

public sealed class DocumentService(IDocumentRepository documents, IObjectStorage storage)
{
    public async Task<DocumentDto> UploadAsync(string ownerId, string fileName, string contentType, Stream content,
        DocumentKind kind, CancellationToken ct)
    {
        // Buffer once to hash (tamper evidence) and know the size before storing.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();

        var document = StoredDocument.Create(ownerId, fileName, contentType, buffer.Length, hash, kind);
        buffer.Position = 0;
        await storage.PutAsync(document.StorageKey, buffer, contentType, ct);

        await documents.AddAsync(document, ct);
        await documents.SaveChangesAsync(ct);
        return DocumentDto.From(document);
    }

    public async Task<DocumentDto> GetAsync(string userId, bool isStaff, Guid id, CancellationToken ct) =>
        DocumentDto.From(await LoadReadableAsync(userId, isStaff, id, ct));

    public async Task<(DocumentDto Document, Stream Content)> OpenAsync(string userId, bool isStaff, Guid id,
        CancellationToken ct)
    {
        var document = await LoadReadableAsync(userId, isStaff, id, ct);
        return (DocumentDto.From(document), await storage.GetAsync(document.StorageKey, ct));
    }

    private async Task<StoredDocument> LoadReadableAsync(string userId, bool isStaff, Guid id, CancellationToken ct)
    {
        var document = await documents.GetAsync(id, ct) ?? throw new NotFoundException("errors.document.notFound");
        if (!document.CanBeReadBy(userId, isStaff)) throw new NotFoundException("errors.document.notFound");
        return document;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsApplication(this IServiceCollection services) =>
        services.AddScoped<DocumentService>();
}
