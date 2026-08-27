namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record PurchaseOrderIssuedIntegrationEvent(
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid MaterialRequestId,
    Guid SupplierId,
    Guid DestinationOrganizationUnitId,
    string Currency,
    IReadOnlyCollection<PurchaseOrderIssuedItem> Items) : IntegrationEvent;

public sealed record PurchaseOrderIssuedItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
