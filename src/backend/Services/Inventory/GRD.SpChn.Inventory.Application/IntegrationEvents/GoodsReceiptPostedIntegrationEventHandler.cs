using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Inventory.Application.Stock.ReceivePurchasedStock;
using MediatR;

namespace GRD.SpChn.Inventory.Application.IntegrationEvents;

public sealed class GoodsReceiptPostedIntegrationEventHandler(ISender sender)
    : IIntegrationEventHandler<GoodsReceiptPostedIntegrationEvent>
{
    public async Task HandleAsync(
        GoodsReceiptPostedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ReceivePurchasedStockCommand(
            integrationEvent.EventId,
            integrationEvent.GoodsReceiptId,
            integrationEvent.DestinationOrganizationUnitId,
            integrationEvent.Items.Select(item =>
                new ReceivePurchasedStockItem(item.ProductId, item.Quantity)).ToArray()),
            cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }
}
