using Dsp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dsp.Documents.Infrastructure;

// One subclass per provider so SQL Server (FedNet) and PostgreSQL (POC) each keep their own migrations.

public sealed class DocumentsSqlServerDbContext(DbContextOptions<DocumentsSqlServerDbContext> options) : DocumentsDbContext(options);

public sealed class DocumentsPostgresDbContext(DbContextOptions<DocumentsPostgresDbContext> options) : DocumentsDbContext(options);

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class DocumentsSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<DocumentsSqlServerDbContext>
{
    public DocumentsSqlServerDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.SqlServer<DocumentsSqlServerDbContext>());
}

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class DocumentsPostgresDesignTimeFactory : IDesignTimeDbContextFactory<DocumentsPostgresDbContext>
{
    public DocumentsPostgresDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.Postgres<DocumentsPostgresDbContext>());
}
