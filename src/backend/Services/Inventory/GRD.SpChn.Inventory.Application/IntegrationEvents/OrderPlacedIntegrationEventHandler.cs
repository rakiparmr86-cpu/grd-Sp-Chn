using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Inventory.Application.Stock.ReserveStock;
using MediatR;

namespace GRD.SpChn.Inventory.Application.IntegrationEvents;

public sealed class OrderPlacedIntegrationEventHandler(ISender sender)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task HandleAsync(
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var command = new ReserveStockCommand(
            integrationEvent.EventId,
            integrationEvent.OrderId,
            integrationEvent.Items
                .Select(item => new ReserveStockItem(item.ProductId, item.Quantity))
                .ToArray());
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"The OrderPlaced event could not be processed: {errors}");
        }
    }
}
