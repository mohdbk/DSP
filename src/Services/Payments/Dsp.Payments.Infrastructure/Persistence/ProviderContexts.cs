using Dsp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dsp.Payments.Infrastructure;

// One subclass per provider so SQL Server (FedNet) and PostgreSQL (POC) each keep their own migrations.

public sealed class PaymentsSqlServerDbContext(DbContextOptions<PaymentsSqlServerDbContext> options) : PaymentsDbContext(options);

public sealed class PaymentsPostgresDbContext(DbContextOptions<PaymentsPostgresDbContext> options) : PaymentsDbContext(options);

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class PaymentsSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<PaymentsSqlServerDbContext>
{
    public PaymentsSqlServerDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.SqlServer<PaymentsSqlServerDbContext>());
}

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class PaymentsPostgresDesignTimeFactory : IDesignTimeDbContextFactory<PaymentsPostgresDbContext>
{
    public PaymentsPostgresDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.Postgres<PaymentsPostgresDbContext>());
}
