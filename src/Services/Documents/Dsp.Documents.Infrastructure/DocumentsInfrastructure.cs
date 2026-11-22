using Dsp.BuildingBlocks.Persistence;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Dsp.Documents.Application;
using Dsp.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dsp.Documents.Infrastructure;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<StoredDocument> Documents => Set<StoredDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("documents");
        modelBuilder.Entity<StoredDocument>(e =>
        {
            e.ToTable("Documents");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.OwnerId).HasMaxLength(200);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.StorageKey).HasMaxLength(300);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        });
    }
}

public sealed class DocumentRepository(DocumentsDbContext db) : IDocumentRepository
{
    public Task<StoredDocument?> GetAsync(Guid id, CancellationToken ct) =>
        db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddAsync(StoredDocument document, CancellationToken ct) => await db.Documents.AddAsync(document, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class StorageOptions
{
    /// <summary>S3 endpoint, e.g. http://seaweedfs.railway.internal:8333. Empty uses local disk (dev only).</summary>
    public string? ServiceUrl { get; set; }
    public string Bucket { get; set; } = "dsp-documents";
    public string AccessKey { get; set; } = "dsp";
    public string SecretKey { get; set; } = "dsp";
    public string LocalPath { get; set; } = "/tmp/dsp-documents";
}

/// <summary>SeaweedFS through its S3 API. Any S3-compatible store works without code changes.</summary>
public sealed class S3ObjectStorage(IAmazonS3 s3, StorageOptions options, ILogger<S3ObjectStorage> logger)
    : IObjectStorage
{
    private bool _bucketReady;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            UseChunkEncoding = false,
            AutoCloseStream = false,
        }, ct);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken ct)
    {
        var response = await s3.GetObjectAsync(options.Bucket, key, ct);
        return response.ResponseStream;
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketReady) return;
        try
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = options.Bucket }, ct);
            logger.LogInformation("Created bucket {Bucket}", options.Bucket);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
        _bucketReady = true;
    }
}

/// <summary>Local disk storage for running without SeaweedFS (development only).</summary>
public sealed class LocalObjectStorage(StorageOptions options) : IObjectStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Path.Combine(options.LocalPath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> GetAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(options.LocalPath, key)));
}

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<DocumentsDbContext>(o =>
            o.UseDspDatabase(configuration, "Documents"));
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        var storage = new StorageOptions();
        configuration.GetSection("Storage").Bind(storage);
        services.AddSingleton(storage);

        if (string.IsNullOrWhiteSpace(storage.ServiceUrl))
        {
            services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        }
        else
        {
            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
                new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = storage.ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = "us-east-1",
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                }));
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        }

        return services;
    }
}
