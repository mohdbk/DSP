using Dsp.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dsp.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Registers the event bus. Uses RabbitMQ when <c>RabbitMq:Url</c> is set, otherwise a logging bus.
    /// <paramref name="queueName"/> is the durable queue this service consumes from.
    /// </summary>
    public static IServiceCollection AddDspMessaging(this IServiceCollection services, IConfiguration configuration,
        string queueName)
    {
        var options = new RabbitMqOptions { ClientName = queueName };
        configuration.GetSection("RabbitMq").Bind(options);
        services.TryAddSingleton(new SubscriptionRegistry { QueueName = queueName });

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            services.AddSingleton<IEventBus, LoggingEventBus>();
            return services;
        }

        services.AddSingleton(options);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        services.AddHostedService<RabbitMqSubscriber>();
        return services;
    }

    /// <summary>Subscribes this service to <typeparamref name="TEvent"/> using <typeparamref name="THandler"/>.</summary>
    public static IServiceCollection AddEventSubscription<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        var registry = services
                           .Where(d => !d.IsKeyedService && d.ServiceType == typeof(SubscriptionRegistry))
                           .Select(d => d.ImplementationInstance)
                           .OfType<SubscriptionRegistry>()
                           .FirstOrDefault()
                       ?? throw new InvalidOperationException("Call AddDspMessaging before AddEventSubscription.");
        registry.EventTypes[typeof(TEvent).Name] = typeof(TEvent);
        return services;
    }
}
