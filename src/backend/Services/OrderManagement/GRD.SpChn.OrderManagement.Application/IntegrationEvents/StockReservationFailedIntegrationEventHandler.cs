using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.OrderManagement.Application.Orders;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.IntegrationEvents;

public sealed class StockReservationFailedIntegrationEventHandler(OrderProcessManager processManager)
    : IIntegrationEventHandler<StockReservationFailedIntegrationEvent>
{
    public Task HandleAsync(
        StockReservationFailedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        processManager.ProcessReservationResultAsync(
            integrationEvent.EventId,
            nameof(StockReservationFailedIntegrationEvent),
            integrationEvent.OrderId,
            OrderStatus.Cancelled,
            cancellationToken);
}
