using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Inventory.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.ReceivePurchasedStock;

internal sealed class ReceivePurchasedStockCommandHandler(
    IInboxStore inboxStore,
    ILocationInventoryRepository repository)
    : IRequestHandler<ReceivePurchasedStockCommand, Result<ReceivePurchasedStockResponse>>
{
    public async Task<Result<ReceivePurchasedStockResponse>> Handle(
        ReceivePurchasedStockCommand request,
        CancellationToken cancellationToken)
    {
        if (request.EventId == Guid.Empty ||
            request.QualityInspectionId == Guid.Empty ||
            request.GoodsReceiptId == Guid.Empty ||
            request.DestinationOrganizationUnitId == Guid.Empty ||
            request.Items.Count == 0)
        {
            return Result<ReceivePurchasedStockResponse>.Failure(Error.Validation(
                "Inventory.InvalidGoodsReceipt",
                "The goods receipt event is missing required identifiers or items."));
        }

        LocationStockReceipt[] receipts;
        try
        {
            receipts = request.Items
                .Select(item => LocationStockReceipt.Create(
                    request.DestinationOrganizationUnitId,
                    item.ProductId,
                    item.Quantity))
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            return Result<ReceivePurchasedStockResponse>.Failure(Error.Validation(
                "Inventory.InvalidGoodsReceiptItem",
                exception.Message));
        }

        var isNew = await inboxStore.TryAddAsync(
            request.EventId,
            nameof(QualityInspectionApprovedIntegrationEvent),
            cancellationToken);
        if (!isNew)
        {
            return Result<ReceivePurchasedStockResponse>.Success(
                new ReceivePurchasedStockResponse(request.GoodsReceiptId, true));
        }

        foreach (var receipt in receipts)
        {
            await repository.ReceiveAsync(
                receipt,
                request.EventId,
                request.QualityInspectionId,
                cancellationToken);
        }

        return Result<ReceivePurchasedStockResponse>.Success(
            new ReceivePurchasedStockResponse(request.GoodsReceiptId, false));
    }
}
