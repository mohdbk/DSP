using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dsp.BuildingBlocks.Persistence;

public static class DatabaseStartup
{
    /// <summary>
    /// Creates the service's database and schema if missing, retrying while SQL Server starts.
    /// POC only: production uses reviewed EF Core migrations applied by the pipeline.
    /// </summary>
    public static async Task EnsureDatabaseAsync<TContext>(this WebApplication app) where TContext : DbContext
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Dsp.Database");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("Database for {Context} is ready", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < 40)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}), retrying", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
