using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.IntegrationEvents;

public sealed class StockReservedIntegrationEventHandler(IOrderRepository repository)
    : IIntegrationEventHandler<StockReservedIntegrationEvent>
{
    public Task HandleAsync(
        StockReservedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        repository.ApplyReservationResultAsync(
            integrationEvent.EventId,
            nameof(StockReservedIntegrationEvent),
            integrationEvent.OrderId,
            OrderStatus.Confirmed,
            cancellationToken);
}
