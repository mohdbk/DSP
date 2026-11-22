using Dsp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Registers reliable messaging for a service backed by <typeparamref name="TContext"/>:
    /// outbox-based publishing, the outbox dispatcher, the idempotent inbox and the RabbitMQ consumer.
    /// The context's model must call <see cref="MessagingModel.AddMessagingTables"/>.
    /// </summary>
    public static IServiceCollection AddDspMessaging<TContext>(this IServiceCollection services,
        IConfiguration configuration, string serviceName) where TContext : DbContext
    {
        var options = new RabbitMqOptions { ClientName = serviceName };
        configuration.GetSection("RabbitMq").Bind(options);

        services.AddSingleton(new SubscriptionRegistry { ServiceName = serviceName });
        services.AddScoped<IEventBus, OutboxEventBus<TContext>>();
        services.AddScoped<IInboxStore, EfInboxStore<TContext>>();
        services.AddHostedService<OutboxDispatcher<TContext>>();

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            services.AddSingleton<IMessagePublisher, LoggingPublisher>();
            return services;
        }

        services.AddSingleton(options);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
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
