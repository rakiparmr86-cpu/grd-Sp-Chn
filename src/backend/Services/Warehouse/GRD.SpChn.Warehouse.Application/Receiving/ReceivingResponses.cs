using GRD.SpChn.Warehouse.Domain;

namespace GRD.SpChn.Warehouse.Application.Receiving;

public sealed record ExpectedPurchaseOrderResponse(
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid SupplierId,
    Guid DestinationOrganizationUnitId,
    string Status,
    IReadOnlyCollection<ExpectedPurchaseOrderItemResponse> Items)
{
    public static ExpectedPurchaseOrderResponse From(ExpectedPurchaseOrder order) =>
        new(
            order.PurchaseOrderId,
            order.PurchaseOrderNumber,
            order.SupplierId,
            order.DestinationOrganizationUnitId,
            order.Status.ToString(),
            order.Items.Select(item => new ExpectedPurchaseOrderItemResponse(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure)).ToArray());
}

public sealed record ExpectedPurchaseOrderItemResponse(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);

public sealed record GoodsReceiptResponse(
    Guid Id,
    string GoodsReceiptNumber,
    Guid PurchaseOrderId,
    Guid DestinationOrganizationUnitId,
    Guid ReceivedByUserId,
    DateTime ReceivedOnUtc,
    IReadOnlyCollection<ExpectedPurchaseOrderItemResponse> Items)
{
    public static GoodsReceiptResponse From(GoodsReceipt receipt) =>
        new(
            receipt.Id,
            receipt.GoodsReceiptNumber,
            receipt.PurchaseOrderId,
            receipt.DestinationOrganizationUnitId,
            receipt.ReceivedByUserId,
            receipt.ReceivedOnUtc,
            receipt.Items.Select(item => new ExpectedPurchaseOrderItemResponse(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure)).ToArray());
}
