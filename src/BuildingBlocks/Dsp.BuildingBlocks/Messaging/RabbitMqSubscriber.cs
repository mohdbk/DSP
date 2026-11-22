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
    public required string QueueName { get; init; }
    public Dictionary<string, Type> EventTypes { get; } = new(StringComparer.Ordinal);
}

/// <summary>Consumes the service's queue and dispatches each message to its handler in a fresh DI scope.</summary>
public sealed class RabbitMqSubscriber(
    RabbitMqConnection connection,
    SubscriptionRegistry registry,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqSubscriber> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (registry.EventTypes.Count == 0) return;

        var conn = await connection.GetAsync(stoppingToken);
        _channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(connection.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(registry.QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
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
            var @event = JsonSerializer.Deserialize(ea.Body.Span, eventType, MessagingJson.Options)
                         ?? throw new InvalidOperationException("Empty message body");

            await using var scope = scopeFactory.CreateAsyncScope();
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handler = scope.ServiceProvider.GetRequiredService(handlerType);
            var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;
            await (Task)method.Invoke(handler, [@event, ct])!;

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Requeue once; a redelivered message that fails again is dropped to avoid a poison loop.
            logger.LogError(ex, "Failed handling {EventType}", ea.RoutingKey);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: !ea.Redelivered,
                cancellationToken: ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
