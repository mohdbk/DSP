using Dsp.Identity.Application;
using Dsp.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dsp.Identity.Infrastructure.Persistence;

/// <summary>The service model. Provider-specific subclasses carry their own migrations.</summary>
public abstract class IdentityDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.Entity<StaffAccount>(e =>
        {
            e.ToTable("StaffAccounts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
        });
    }
}

public sealed class StaffAccountRepository(IdentityDbContext db) : IStaffAccountRepository
{
    public Task<StaffAccount?> FindByUsernameAsync(string username, CancellationToken ct) =>
        db.StaffAccounts.FirstOrDefaultAsync(a => a.Username == username, ct);

    public Task<bool> AnyAsync(CancellationToken ct) => db.StaffAccounts.AnyAsync(ct);

    public async Task AddAsync(StaffAccount account, CancellationToken ct) => await db.StaffAccounts.AddAsync(account, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
