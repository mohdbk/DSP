using Dsp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dsp.EServices.Infrastructure.Persistence;

// One subclass per provider so SQL Server (FedNet) and PostgreSQL (POC) each keep their own migrations.

public sealed class EServicesSqlServerDbContext(DbContextOptions<EServicesSqlServerDbContext> options) : EServicesDbContext(options);

public sealed class EServicesPostgresDbContext(DbContextOptions<EServicesPostgresDbContext> options) : EServicesDbContext(options);

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class EServicesSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<EServicesSqlServerDbContext>
{
    public EServicesSqlServerDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.SqlServer<EServicesSqlServerDbContext>());
}

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class EServicesPostgresDesignTimeFactory : IDesignTimeDbContextFactory<EServicesPostgresDbContext>
{
    public EServicesPostgresDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.Postgres<EServicesPostgresDbContext>());
}
