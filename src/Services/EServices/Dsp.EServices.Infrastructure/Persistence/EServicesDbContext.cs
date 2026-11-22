using Dsp.BuildingBlocks.Messaging;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Dsp.EServices.Infrastructure.Persistence;

/// <summary>The service model. Provider-specific subclasses carry their own migrations.</summary>
public abstract class EServicesDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ServiceApplication> Applications => Set<ServiceApplication>();
    public DbSet<ReviewTask> ReviewTasks => Set<ReviewTask>();

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
        });
    }
}

public sealed class ApplicationRepository(EServicesDbContext db) : IApplicationRepository
{
    private static readonly ApplicationStatus[] ActiveStatuses =
    [
        ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.AwaitingPayment,
        ApplicationStatus.Paid, ApplicationStatus.Approved,
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
