namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record GoodsReceiptPostedIntegrationEvent(
    Guid GoodsReceiptId,
    string GoodsReceiptNumber,
    Guid PurchaseOrderId,
    Guid DestinationOrganizationUnitId,
    Guid ReceivedByUserId,
    IReadOnlyCollection<GoodsReceiptPostedItem> Items) : IntegrationEvent;

public sealed record GoodsReceiptPostedItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);
