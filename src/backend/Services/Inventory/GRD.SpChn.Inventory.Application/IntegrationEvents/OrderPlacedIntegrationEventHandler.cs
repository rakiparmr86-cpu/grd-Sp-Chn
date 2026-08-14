using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Inventory.Application.Abstractions;

namespace GRD.SpChn.Inventory.Application.IntegrationEvents;

public sealed class OrderPlacedIntegrationEventHandler(IInventoryRepository repository)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task HandleAsync(
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await repository.ReserveForOrderAsync(integrationEvent, cancellationToken);
    }
}
