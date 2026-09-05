using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.Procurement.Application.IntegrationEvents;
using GRD.SpChn.Procurement.Infrastructure.Inbox;
using GRD.SpChn.Procurement.Infrastructure.Outbox;
using GRD.SpChn.Procurement.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Procurement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<ProcurementUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ProcurementUnitOfWork>());
        services.AddScoped<IProcurementRepository, ProcurementRepository>();
        services.AddScoped<IOutboxWriter, ProcurementOutboxWriter>();
        services.AddScoped<IInboxStore, ProcurementInboxStore>();
        services.AddRabbitMqConsumer<
            QualityInspectionApprovedIntegrationEvent,
            QualityInspectionApprovedIntegrationEventHandler>(
            MessagingTopology.WarehouseExchange,
            "procurement.quality-inspection-approved",
            MessagingTopology.QualityInspectionApprovedRoutingKey);

        return services;
    }
}
