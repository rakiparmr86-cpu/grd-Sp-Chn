using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.OrderManagement.Application.Orders;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.IntegrationEvents;

public sealed class StockReservedIntegrationEventHandler(OrderProcessManager processManager)
    : IIntegrationEventHandler<StockReservedIntegrationEvent>
{
    public Task HandleAsync(
        StockReservedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        processManager.ProcessReservationResultAsync(
            integrationEvent.EventId,
            nameof(StockReservedIntegrationEvent),
            integrationEvent.OrderId,
            OrderStatus.Confirmed,
            cancellationToken);
}
