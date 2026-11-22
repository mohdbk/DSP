using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Dsp.BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public string? Url { get; set; }
    public string Exchange { get; set; } = "dsp.events";
    public string ClientName { get; set; } = "dsp";
}

/// <summary>Owns a single, lazily created, auto-recovering connection per process.</summary>
public sealed class RabbitMqConnection(RabbitMqOptions options, ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public string Exchange => options.Exchange;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(options.Url!),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(options.ClientName, ct);
                    logger.LogInformation("Connected to RabbitMQ");
                    return _connection;
                }
                catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "RabbitMQ not reachable (attempt {Attempt}), retrying", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
