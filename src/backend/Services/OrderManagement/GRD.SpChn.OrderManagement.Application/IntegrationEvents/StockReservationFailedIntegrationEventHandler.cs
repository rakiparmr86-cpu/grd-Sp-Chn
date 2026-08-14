using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.IntegrationEvents;

public sealed class StockReservationFailedIntegrationEventHandler(IOrderRepository repository)
    : IIntegrationEventHandler<StockReservationFailedIntegrationEvent>
{
    public Task HandleAsync(
        StockReservationFailedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        repository.ApplyReservationResultAsync(
            integrationEvent.EventId,
            nameof(StockReservationFailedIntegrationEvent),
            integrationEvent.OrderId,
            OrderStatus.Cancelled,
            cancellationToken);
}
