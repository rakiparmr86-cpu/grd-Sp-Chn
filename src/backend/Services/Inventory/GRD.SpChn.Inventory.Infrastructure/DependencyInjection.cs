using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Inventory.Application.IntegrationEvents;
using GRD.SpChn.Persistence.MySql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddRabbitMqConsumer<
            OrderPlacedIntegrationEvent,
            OrderPlacedIntegrationEventHandler>(
            MessagingTopology.OrderExchange,
            "inventory.order-placed",
            MessagingTopology.OrderPlacedRoutingKey);

        return services;
    }
}
