namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record QualityInspectionApprovedIntegrationEvent(
    Guid QualityInspectionId,
    Guid GoodsReceiptId,
    string GoodsReceiptNumber,
    Guid PurchaseOrderId,
    Guid DestinationOrganizationUnitId,
    Guid InspectedByUserId,
    IReadOnlyCollection<QualityApprovedItem> Items) : IntegrationEvent;

public sealed record QualityApprovedItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);
