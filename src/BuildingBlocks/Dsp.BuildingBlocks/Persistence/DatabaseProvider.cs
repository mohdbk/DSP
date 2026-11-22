using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.BuildingBlocks.Persistence;

/// <summary>
/// Selects the relational provider from Database:Provider. SQL Server is the default (FedNet);
/// PostgreSQL is used where SQL Server can't run (the Railway POC).
///
/// Each service has one abstract DbContext (the model) and two thin provider-specific subclasses,
/// so each provider gets its own migration history in the same assembly.
/// </summary>
public static class DatabaseProvider
{
    public const string SqlServer = "SqlServer";
    public const string Postgres = "Postgres";

    public static bool IsPostgres(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], Postgres, StringComparison.OrdinalIgnoreCase);

    /// <summary>Registers <typeparamref name="TContext"/> backed by the provider-specific subclass.</summary>
    public static IServiceCollection AddDspDbContext<TContext, TSqlServer, TPostgres>(this IServiceCollection services,
        IConfiguration configuration, string connectionName)
        where TContext : DbContext
        where TSqlServer : TContext
        where TPostgres : TContext
    {
        var connectionString = configuration.GetConnectionString(connectionName)
                               ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is required.");

        if (IsPostgres(configuration))
            services.AddDbContext<TContext, TPostgres>(o => o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure()));
        else
            services.AddDbContext<TContext, TSqlServer>(o => o.UseSqlServer(connectionString, s => s.EnableRetryOnFailure()));

        return services;
    }
}

/// <summary>Builds provider-specific options for design-time factories (dotnet ef). No connection is opened.</summary>
public static class DesignTimeOptions
{
    public static DbContextOptions<T> SqlServer<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer("Server=design-time;Database=design-time").Options;

    public static DbContextOptions<T> Postgres<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql("Host=design-time;Database=design-time").Options;
}
