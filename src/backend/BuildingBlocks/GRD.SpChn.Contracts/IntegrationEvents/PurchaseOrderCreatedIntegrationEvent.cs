namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record PurchaseOrderCreatedIntegrationEvent(Guid PurchaseOrderId, string PurchaseOrderNumber, Guid SupplierId) : IntegrationEvent;
