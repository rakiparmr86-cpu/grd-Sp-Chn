namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record GoodsReceivedIntegrationEvent(Guid GoodsReceiptId, Guid PurchaseOrderId, Guid WarehouseId) : IntegrationEvent;
