using Dsp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dsp.Identity.Infrastructure.Persistence;

// One subclass per provider so SQL Server (FedNet) and PostgreSQL (POC) each keep their own migrations.

public sealed class IdentitySqlServerDbContext(DbContextOptions<IdentitySqlServerDbContext> options) : IdentityDbContext(options);

public sealed class IdentityPostgresDbContext(DbContextOptions<IdentityPostgresDbContext> options) : IdentityDbContext(options);

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class IdentitySqlServerDesignTimeFactory : IDesignTimeDbContextFactory<IdentitySqlServerDbContext>
{
    public IdentitySqlServerDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.SqlServer<IdentitySqlServerDbContext>());
}

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class IdentityPostgresDesignTimeFactory : IDesignTimeDbContextFactory<IdentityPostgresDbContext>
{
    public IdentityPostgresDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.Postgres<IdentityPostgresDbContext>());
}
