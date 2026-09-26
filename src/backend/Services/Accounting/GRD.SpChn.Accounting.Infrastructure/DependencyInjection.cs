using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Application.IntegrationEvents;
using GRD.SpChn.Accounting.Application.Ledger;
using GRD.SpChn.Accounting.Infrastructure.Inbox;
using GRD.SpChn.Accounting.Infrastructure.Outbox;
using GRD.SpChn.Accounting.Infrastructure.Persistence;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Persistence.MySql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Accounting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<AccountingUnitOfWork>();
        services.AddScoped<IAccountingUnitOfWork>(provider =>
            provider.GetRequiredService<AccountingUnitOfWork>());
        services.AddScoped<IAccountingRepository, AccountingRepository>();
        services.AddScoped<IAccountLedgerReader, AccountLedgerReader>();
        services.AddScoped<IAccountBalancesReader, AccountBalancesReader>();
        services.AddScoped<IAccountingInboxStore, AccountingInboxStore>();
        services.AddScoped<IAccountingOutboxWriter, AccountingOutboxWriter>();
        services.AddRabbitMqConsumer<
            PurchaseOrderIssuedIntegrationEvent,
            PurchaseOrderIssuedIntegrationEventHandler>(
            MessagingTopology.ProcurementExchange,
            "accounting.purchase-order-issued",
            MessagingTopology.PurchaseOrderIssuedRoutingKey);
        services.AddRabbitMqConsumer<
            QualityInspectionApprovedIntegrationEvent,
            QualityInspectionApprovedIntegrationEventHandler>(
            MessagingTopology.WarehouseExchange,
            "accounting.quality-inspection-approved",
            MessagingTopology.QualityInspectionApprovedRoutingKey);
        return services;
    }
}
