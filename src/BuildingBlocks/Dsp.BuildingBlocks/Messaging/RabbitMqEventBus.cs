using System.Text.Json;
using Dsp.Contracts;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Dsp.BuildingBlocks.Messaging;

/// <summary>Publishes integration events to a topic exchange. Routing key = event type name.</summary>
public sealed class RabbitMqEventBus(RabbitMqConnection connection, ILogger<RabbitMqEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        var conn = await connection.GetAsync(cancellationToken);
        await using var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(connection.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        var routingKey = typeof(TEvent).Name;
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, MessagingJson.Options);
        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = @event.EventId.ToString(),
            Type = routingKey,
        };

        await channel.BasicPublishAsync(connection.Exchange, routingKey, mandatory: false, basicProperties: props,
            body: body, cancellationToken: cancellationToken);

        logger.LogInformation("Published {EventType} {EventId}", routingKey, @event.EventId);
    }
}

/// <summary>Used when no broker is configured (local runs). Logs instead of publishing.</summary>
public sealed class LoggingEventBus(ILogger<LoggingEventBus> logger) : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        logger.LogWarning("No event bus configured; {EventType} not published: {@Event}", typeof(TEvent).Name, @event);
        return Task.CompletedTask;
    }
}

internal static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
