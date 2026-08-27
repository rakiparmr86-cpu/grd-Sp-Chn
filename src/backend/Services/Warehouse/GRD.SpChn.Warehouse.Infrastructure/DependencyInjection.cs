using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Application.IntegrationEvents;
using GRD.SpChn.Warehouse.Infrastructure.Inbox;
using GRD.SpChn.Warehouse.Infrastructure.Outbox;
using GRD.SpChn.Warehouse.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Warehouse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<WarehouseUnitOfWork>();
        services.AddScoped<IWarehouseUnitOfWork>(provider => provider.GetRequiredService<WarehouseUnitOfWork>());
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IWarehouseInboxStore, WarehouseInboxStore>();
        services.AddScoped<IWarehouseOutboxWriter, WarehouseOutboxWriter>();
        services.AddRabbitMqConsumer<
            PurchaseOrderIssuedIntegrationEvent,
            PurchaseOrderIssuedIntegrationEventHandler>(
            MessagingTopology.ProcurementExchange,
            "warehouse.purchase-order-issued",
            MessagingTopology.PurchaseOrderIssuedRoutingKey);

        return services;
    }
}
