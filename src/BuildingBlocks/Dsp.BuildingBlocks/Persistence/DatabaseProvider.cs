using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Dsp.BuildingBlocks.Persistence;

/// <summary>
/// Selects the relational provider from Database:Provider. SQL Server is the default (FedNet);
/// PostgreSQL is used where SQL Server can't run (the Railway POC). Code and models are the same.
/// </summary>
public static class DatabaseProvider
{
    public const string SqlServer = "SqlServer";
    public const string Postgres = "Postgres";

    public static bool IsPostgres(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], Postgres, StringComparison.OrdinalIgnoreCase);

    public static DbContextOptionsBuilder UseDspDatabase(this DbContextOptionsBuilder options,
        IConfiguration configuration, string connectionName)
    {
        var connectionString = configuration.GetConnectionString(connectionName)
                               ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is required.");

        return IsPostgres(configuration)
            ? options.UseNpgsql(connectionString, o => o.EnableRetryOnFailure())
            : options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure());
    }
}
