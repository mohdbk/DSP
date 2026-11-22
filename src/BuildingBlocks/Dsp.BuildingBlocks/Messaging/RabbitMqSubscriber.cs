using System.Text.Json;
using Dsp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Dsp.BuildingBlocks.Messaging;

/// <summary>Which events this service consumes, and from which durable queue.</summary>
public sealed class SubscriptionRegistry
{
    public required string ServiceName { get; init; }
    public Dictionary<string, Type> EventTypes { get; } = new(StringComparer.Ordinal);

    public string QueueName => $"dsp.{ServiceName}";
    public string DeadLetterQueueName => $"dsp.{ServiceName}.dead-letter";

    /// <summary>Queue name used before dead-lettering was added; removed on startup.</summary>
    public string LegacyQueueName => ServiceName;
}

/// <summary>
/// Consumes the service's queue. Each message is handled in its own DI scope, skipped if the inbox
/// says it was already handled, retried once on failure, then moved to the dead-letter queue (never dropped).
/// </summary>
public sealed class RabbitMqSubscriber(
    RabbitMqConnection connection,
    SubscriptionRegistry registry,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqSubscriber> logger) : BackgroundService
{
    public const string DeadLetterExchange = "dsp.dead-letter";
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (registry.EventTypes.Count == 0) return;

        var conn = await connection.GetAsync(stoppingToken);
        await RemoveLegacyQueueAsync(conn, stoppingToken);

        _channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(connection.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);

        // Dead-letter topology: failed messages are kept for inspection and replay.
        await _channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(registry.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(registry.DeadLetterQueueName, DeadLetterExchange, registry.QueueName,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(registry.QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = registry.QueueName,
            },
            cancellationToken: stoppingToken);
        foreach (var routingKey in registry.EventTypes.Keys)
        {
            await _channel.QueueBindAsync(registry.QueueName, connection.Exchange, routingKey,
                cancellationToken: stoppingToken);
        }
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleAsync(ea, stoppingToken);
        await _channel.BasicConsumeAsync(registry.QueueName, autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Consuming {Queue} for {Events}", registry.QueueName,
            string.Join(", ", registry.EventTypes.Keys));

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var channel = _channel!;
        if (!registry.EventTypes.TryGetValue(ea.RoutingKey, out var eventType))
        {
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            return;
        }

        try
        {
            var @event = (IIntegrationEvent)(JsonSerializer.Deserialize(ea.Body.Span, eventType, MessagingJson.Options)
                                             ?? throw new InvalidOperationException("Empty message body"));

            await using var scope = scopeFactory.CreateAsyncScope();
            var inbox = scope.ServiceProvider.GetService<IInboxStore>();
            if (inbox is not null && await inbox.HasProcessedAsync(@event.EventId, ct))
            {
                logger.LogInformation("Skipping duplicate {EventType} {EventId}", ea.RoutingKey, @event.EventId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                return;
            }
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handler = scope.ServiceProvider.GetRequiredService(handlerType);
            var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;
            await (Task)method.Invoke(handler, [@event, ct])!;

            // Recorded only after the handler fully succeeds, so a failure part-way is retried.
            // Handlers are written to be safe to run again (they check current state first).
            if (inbox is not null)
            {
                inbox.MarkProcessed(@event.EventId, ea.RoutingKey);
                await inbox.SaveChangesAsync(ct);
            }

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // First failure: requeue. Second failure: reject without requeue, which dead-letters the message.
            logger.LogError(ex, "Failed handling {EventType} (redelivered: {Redelivered})", ea.RoutingKey, ea.Redelivered);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: !ea.Redelivered,
                cancellationToken: ct);
        }
    }

    private async Task RemoveLegacyQueueAsync(IConnection conn, CancellationToken ct)
    {
        try
        {
            await using var ch = await conn.CreateChannelAsync(cancellationToken: ct);
            await ch.QueueDeleteAsync(registry.LegacyQueueName, ifUnused: false, ifEmpty: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug("Legacy queue cleanup skipped: {Error}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
