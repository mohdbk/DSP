using Dsp.BuildingBlocks.Messaging;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Dsp.EServices.Infrastructure.Persistence;

/// <summary>The service model. Provider-specific subclasses carry their own migrations.</summary>
public abstract class EServicesDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ServiceApplication> Applications => Set<ServiceApplication>();
    public DbSet<ReviewTask> ReviewTasks => Set<ReviewTask>();
    public DbSet<ServiceVersion> ServiceVersions => Set<ServiceVersion>();
    public DbSet<AiAssessment> AiAssessments => Set<AiAssessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("eservices");
        modelBuilder.AddMessagingTables();

        modelBuilder.Entity<ServiceApplication>(e =>
        {
            e.ToTable("Applications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.ApplicantId);
            e.HasIndex(x => new { x.ServiceCode, x.ReferenceValue });
            e.Property(x => x.Number).HasMaxLength(32);
            e.Property(x => x.ServiceCode).HasMaxLength(64);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.Property(x => x.ApplicantId).HasMaxLength(200);
            e.Property(x => x.ApplicantName).HasMaxLength(200);
            e.Property(x => x.ReferenceValue).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Fee).HasPrecision(18, 2);
            e.Property(x => x.DecidedBy).HasMaxLength(200);
            e.Property(x => x.ServiceVersion).HasDefaultValue(1);
        });

        modelBuilder.Entity<ReviewTask>(e =>
        {
            e.ToTable("ReviewTasks");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.ApplicationId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ApplicationNumber).HasMaxLength(32);
            e.Property(x => x.ServiceCode).HasMaxLength(64);
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.Property(x => x.ApplicantName).HasMaxLength(200);
            e.Property(x => x.ReferenceValue).HasMaxLength(200);
            e.Property(x => x.CompletedBy).HasMaxLength(200);
            e.Property(x => x.Outcome).HasMaxLength(32);
            e.Property(x => x.AiRecommendation).HasMaxLength(32);
        });

        modelBuilder.Entity<ServiceVersion>(e =>
        {
            e.ToTable("ServiceVersions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Code, x.Version }).IsUnique();
            e.HasIndex(x => new { x.Code, x.Status });
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.Property(x => x.UpdatedBy).HasMaxLength(200);
            e.Property(x => x.PublishedBy).HasMaxLength(200);
        });

        modelBuilder.Entity<AiAssessment>(e =>
        {
            e.ToTable("AiAssessments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ApplicationId, x.Round });
            e.Property(x => x.Recommendation).HasMaxLength(32);
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.Mode).HasMaxLength(16);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Ignore(x => x.AsksForInformation);
        });
    }
}

public sealed class ApplicationRepository(EServicesDbContext db) : IApplicationRepository
{
    private static readonly ApplicationStatus[] ActiveStatuses =
    [
        ApplicationStatus.Submitted, ApplicationStatus.AiReview, ApplicationStatus.InformationRequested,
        ApplicationStatus.UnderReview, ApplicationStatus.AwaitingPayment, ApplicationStatus.Paid, ApplicationStatus.Approved,
    ];

    public Task<ServiceApplication?> GetAsync(Guid id, CancellationToken ct) =>
        db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<ServiceApplication?> ReadAsync(Guid id, CancellationToken ct) =>
        db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ServiceApplication>> ListForApplicantAsync(string applicantId, CancellationToken ct) =>
        await db.Applications.AsNoTracking().Where(a => a.ApplicantId == applicantId)
            .OrderByDescending(a => a.UpdatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ServiceApplication>> ListAllAsync(int take, CancellationToken ct) =>
        await db.Applications.AsNoTracking().Where(a => a.Status != ApplicationStatus.Draft)
            .OrderByDescending(a => a.UpdatedAt).Take(take).ToListAsync(ct);

    public Task<bool> ReferenceInUseAsync(string serviceCode, string referenceValue, Guid excludeId,
        CancellationToken ct) =>
        db.Applications.AnyAsync(a => a.ServiceCode == serviceCode && a.Id != excludeId &&
                                      a.ReferenceValue == referenceValue && ActiveStatuses.Contains(a.Status), ct);

    public async Task<IReadOnlyList<string>> ListActiveReferencesAsync(string serviceCode, Guid excludeId, int take,
        CancellationToken ct) =>
        await db.Applications.AsNoTracking()
            .Where(a => a.ServiceCode == serviceCode && a.Id != excludeId && a.ReferenceValue != null &&
                        ActiveStatuses.Contains(a.Status))
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => a.ReferenceValue!)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(ServiceApplication application, CancellationToken ct) =>
        await db.Applications.AddAsync(application, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class ReviewTaskRepository(EServicesDbContext db) : IReviewTaskRepository
{
    public Task<ReviewTask?> GetAsync(Guid id, CancellationToken ct) =>
        db.ReviewTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ReviewTask>> ListOpenAsync(CancellationToken ct) =>
        await db.ReviewTasks.AsNoTracking().Where(t => t.Status == ReviewTaskStatus.Open)
            .OrderBy(t => t.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ReviewTask>> ListForApplicationAsync(Guid applicationId, CancellationToken ct) =>
        await db.ReviewTasks.AsNoTracking().Where(t => t.ApplicationId == applicationId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(ReviewTask task, CancellationToken ct) => await db.ReviewTasks.AddAsync(task, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class AiAssessmentRepository(EServicesDbContext db) : IAiAssessmentRepository
{
    /// <summary>Newest round first.</summary>
    public async Task<IReadOnlyList<AiAssessment>> ListForApplicationAsync(Guid applicationId, CancellationToken ct) =>
        (await db.AiAssessments.AsNoTracking().Where(a => a.ApplicationId == applicationId).ToListAsync(ct))
            .OrderByDescending(a => a.Round).ThenByDescending(a => a.CreatedAt).ToList();

    public async Task AddAsync(AiAssessment assessment, CancellationToken ct) => await db.AiAssessments.AddAsync(assessment, ct);
}

/// <summary>Service versions for the builder, and the published catalog for everyone else.</summary>
public sealed class ServiceVersionRepository(EServicesDbContext db) : IServiceVersionRepository, IServiceCatalog
{
    public async Task<IReadOnlyList<ServiceVersion>> ListAllAsync(CancellationToken ct) =>
        await db.ServiceVersions.OrderBy(v => v.Code).ThenByDescending(v => v.Version).ToListAsync(ct);

    public async Task<IReadOnlyList<ServiceVersion>> ListForCodeAsync(string code, CancellationToken ct) =>
        await db.ServiceVersions.Where(v => v.Code == code).OrderByDescending(v => v.Version).ToListAsync(ct);

    public Task<bool> AnyAsync(CancellationToken ct) => db.ServiceVersions.AnyAsync(ct);

    public async Task AddAsync(ServiceVersion version, CancellationToken ct) => await db.ServiceVersions.AddAsync(version, ct);

    public void Remove(ServiceVersion version) => db.ServiceVersions.Remove(version);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<ServiceDefinition>> ListPublishedAsync(CancellationToken ct) =>
        (await db.ServiceVersions.AsNoTracking().Where(v => v.Status == ServiceVersionStatus.Published).ToListAsync(ct))
            .Select(v => v.ToDefinition())
            .OrderBy(d => d.Name)
            .ToList();

    public async Task<ServiceDefinition?> FindPublishedAsync(string code, CancellationToken ct) =>
        (await db.ServiceVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == code && v.Status == ServiceVersionStatus.Published, ct))?.ToDefinition();

    /// <summary>Published or retired versions only: drafts are never used by applications.</summary>
    public async Task<ServiceDefinition?> FindVersionAsync(string code, int version, CancellationToken ct) =>
        (await db.ServiceVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == code && v.Version == version && v.Status != ServiceVersionStatus.Draft, ct))
        ?.ToDefinition();
}
