using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Messaging;
using Dsp.Payments.Application;
using Dsp.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Payments.Infrastructure;

/// <summary>The service model. Provider-specific subclasses carry their own migrations.</summary>
public abstract class PaymentsDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.AddMessagingTables();
        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReferenceId);
            e.HasIndex(x => x.PayerId);
            e.Property(x => x.PayerId).HasMaxLength(200);
            e.Property(x => x.Description).IsLocalizedText().HasMaxLength(2000);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Method).HasMaxLength(50);
            e.Property(x => x.TransactionReference).HasMaxLength(100);
        });
    }
}

public sealed class PaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetAsync(Guid id, CancellationToken ct) => db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Payment>> ListAsync(string payerId, Guid? referenceId, CancellationToken ct) =>
        await db.Payments.AsNoTracking()
            .Where(p => p.PayerId == payerId && (referenceId == null || p.ReferenceId == referenceId))
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct) => await db.Payments.AddAsync(payment, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

/// <summary>POC processor that approves every charge. Replace with the government payment gateway.</summary>
public sealed class SimulatedPaymentProcessor : IPaymentProcessor
{
    public string Method => "Simulated card";

    public Task<string> ChargeAsync(Payment payment, CancellationToken ct) =>
        Task.FromResult($"SIM-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}");
}

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDspDbContext<PaymentsDbContext, PaymentsSqlServerDbContext, PaymentsPostgresDbContext>(configuration, "Payments");
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<IPaymentProcessor, SimulatedPaymentProcessor>();
        services.AddDspMessaging<PaymentsDbContext>(configuration, "payments");
        return services;
    }
}
