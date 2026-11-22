using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dsp.BuildingBlocks.Persistence;

public static class DatabaseStartup
{
    /// <summary>
    /// Applies pending EF Core migrations when Database:MigrateOnStartup is true (POC and test environments).
    /// In FedNet, migrations are applied by the pipeline from the reviewed scripts in deploy/database/scripts,
    /// and the service only checks that its database is reachable and up to date.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this WebApplication app) where TContext : DbContext
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Dsp.Database");
        var migrate = app.Configuration.GetValue("Database:MigrateOnStartup", false);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();
                var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

                if (pending.Count == 0)
                {
                    logger.LogInformation("Database for {Context} is up to date", typeof(TContext).Name);
                }
                else if (migrate)
                {
                    logger.LogInformation("Applying {Count} migration(s) to {Context}: {Migrations}", pending.Count,
                        typeof(TContext).Name, string.Join(", ", pending));
                    await db.Database.MigrateAsync();
                }
                else
                {
                    logger.LogWarning("{Context} has {Count} pending migration(s) and MigrateOnStartup is off: {Migrations}",
                        typeof(TContext).Name, pending.Count, string.Join(", ", pending));
                }
                return;
            }
            catch (Exception ex) when (attempt < 40)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Error}", attempt, ex.GetBaseException().Message);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
