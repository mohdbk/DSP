using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Dsp.BuildingBlocks.Messaging;

/// <summary>Sends one serialized integration event to the broker. Used only by the outbox dispatcher.</summary>
public interface IMessagePublisher
{
    Task PublishAsync(string type, Guid messageId, string payload, CancellationToken ct);
}

/// <summary>
/// Publishes to the topic exchange (routing key = event type) on a confirm-enabled channel:
/// the call returns only after the broker has accepted the message, and throws otherwise.
/// </summary>
public sealed class RabbitMqPublisher(RabbitMqConnection connection, ILogger<RabbitMqPublisher> logger)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(string type, Guid messageId, string payload, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = messageId.ToString(),
                Type = type,
            };
            await channel.BasicPublishAsync(connection.Exchange, type, mandatory: false, basicProperties: props,
                body: Encoding.UTF8.GetBytes(payload), cancellationToken: ct);
            logger.LogInformation("Published {EventType} {EventId}", type, messageId);
        }
        catch
        {
            // Drop the channel so the next attempt starts clean.
            if (_channel is not null) { await _channel.DisposeAsync(); _channel = null; }
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await _channel.ExchangeDeclareAsync(connection.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: ct);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
    }
}

/// <summary>Used when no broker is configured (local runs). Logs and treats the message as sent.</summary>
public sealed class LoggingPublisher(ILogger<LoggingPublisher> logger) : IMessagePublisher
{
    public Task PublishAsync(string type, Guid messageId, string payload, CancellationToken ct)
    {
        logger.LogWarning("No broker configured; {EventType} {EventId} not sent", type, messageId);
        return Task.CompletedTask;
    }
}

internal static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
