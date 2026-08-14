using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Contracts.IntegrationEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.EventBus.RabbitMQ;

public static class DependencyInjection
{
    public static IServiceCollection AddRabbitMqEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<IEventBus, RabbitMqEventBus>();

        return services;
    }

    public static IServiceCollection AddRabbitMqConsumer<TEvent, THandler>(
        this IServiceCollection services,
        string exchangeName,
        string queueName,
        string routingKey)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        services.AddSingleton(
            new RabbitMqSubscription<TEvent>(exchangeName, queueName, routingKey));
        services.AddHostedService<RabbitMqConsumerHostedService<TEvent>>();

        return services;
    }
}
