using Dsp.SharedKernel;

namespace Dsp.Documents.Domain;

public enum DocumentKind
{
    Attachment,
    Certificate,
}

/// <summary>Metadata for a file held in object storage. The bytes live in SeaweedFS, not the database.</summary>
public sealed class StoredDocument : Entity
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedContentTypes =
        ["application/pdf", "image/png", "image/jpeg", "text/html"];

    private StoredDocument() { }

    public string OwnerId { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = "";
    public DocumentKind Kind { get; private set; }
    public string StorageKey { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static StoredDocument Create(string ownerId, string fileName, string contentType, long sizeBytes,
        string sha256, DocumentKind kind)
    {
        if (sizeBytes <= 0) throw new DomainException("The file is empty.");
        if (sizeBytes > MaxSizeBytes) throw new DomainException("Files must be 10 MB or smaller.");
        if (kind == DocumentKind.Attachment && !AllowedContentTypes.Contains(contentType))
            throw new DomainException("Upload a PDF, PNG or JPEG file.");

        var doc = new StoredDocument
        {
            OwnerId = ownerId,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Kind = kind,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        doc.StorageKey = $"{kind.ToString().ToLowerInvariant()}/{doc.CreatedAt:yyyy/MM}/{doc.Id:N}";
        return doc;
    }

    public bool CanBeReadBy(string userId, bool isStaff) => isStaff || OwnerId == userId;
}
