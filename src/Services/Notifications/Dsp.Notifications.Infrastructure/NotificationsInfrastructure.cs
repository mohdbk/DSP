using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Messaging;
using Dsp.Contracts;
using Dsp.Notifications.Application;
using Dsp.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Notifications.Infrastructure;

/// <summary>The service model. Provider-specific subclasses carry their own migrations.</summary>
public abstract class NotificationsDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.AddMessagingTables();
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Recipient, x.CreatedAt });
            e.Property(x => x.Recipient).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.Property(x => x.Link).HasMaxLength(300);
        });
    }
}

public sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<Notification?> GetAsync(Guid id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notification>> ListAsync(IReadOnlyCollection<string> recipients, int take,
        CancellationToken ct) =>
        await db.Notifications.AsNoTracking().Where(n => recipients.Contains(n.Recipient))
            .OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync(ct);

    public async Task AddAsync(Notification notification, CancellationToken ct) =>
        await db.Notifications.AddAsync(notification, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDspDbContext<NotificationsDbContext, NotificationsSqlServerDbContext, NotificationsPostgresDbContext>(configuration, "Notifications");
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddDspMessaging<NotificationsDbContext>(configuration, "notifications");
        services.AddEventSubscription<ApplicationStatusChanged, ApplicationStatusChangedHandler>();
        return services;
    }
}
