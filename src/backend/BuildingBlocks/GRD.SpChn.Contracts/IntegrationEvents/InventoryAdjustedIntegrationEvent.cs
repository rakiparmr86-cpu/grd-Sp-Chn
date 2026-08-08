namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record InventoryAdjustedIntegrationEvent(Guid AdjustmentId, Guid ProductId, Guid WarehouseId, decimal QuantityChange) : IntegrationEvent;
