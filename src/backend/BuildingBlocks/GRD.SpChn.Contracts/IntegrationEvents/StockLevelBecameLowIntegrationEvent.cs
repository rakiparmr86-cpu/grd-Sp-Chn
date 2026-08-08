namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record StockLevelBecameLowIntegrationEvent(Guid ProductId, Guid WarehouseId, decimal AvailableQuantity, decimal ReorderLevel) : IntegrationEvent;
