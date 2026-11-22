using Dsp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dsp.Notifications.Infrastructure;

// One subclass per provider so SQL Server (FedNet) and PostgreSQL (POC) each keep their own migrations.

public sealed class NotificationsSqlServerDbContext(DbContextOptions<NotificationsSqlServerDbContext> options) : NotificationsDbContext(options);

public sealed class NotificationsPostgresDbContext(DbContextOptions<NotificationsPostgresDbContext> options) : NotificationsDbContext(options);

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class NotificationsSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsSqlServerDbContext>
{
    public NotificationsSqlServerDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.SqlServer<NotificationsSqlServerDbContext>());
}

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class NotificationsPostgresDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsPostgresDbContext>
{
    public NotificationsPostgresDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.Postgres<NotificationsPostgresDbContext>());
}
