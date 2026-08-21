using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Application.IntegrationEvents;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.OrderManagement.Infrastructure.Inbox;
using GRD.SpChn.OrderManagement.Infrastructure.Outbox;
using GRD.SpChn.OrderManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.OrderManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<OrderUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<OrderUnitOfWork>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxWriter, OrderOutboxWriter>();
        services.AddScoped<IInboxStore, OrderInboxStore>();
        services.AddRabbitMqConsumer<
            StockReservedIntegrationEvent,
            StockReservedIntegrationEventHandler>(
            MessagingTopology.InventoryExchange,
            "order-management.stock-reserved",
            MessagingTopology.StockReservedRoutingKey);
        services.AddRabbitMqConsumer<
            StockReservationFailedIntegrationEvent,
            StockReservationFailedIntegrationEventHandler>(
            MessagingTopology.InventoryExchange,
            "order-management.stock-reservation-failed",
            MessagingTopology.StockReservationFailedRoutingKey);

        return services;
    }
}
