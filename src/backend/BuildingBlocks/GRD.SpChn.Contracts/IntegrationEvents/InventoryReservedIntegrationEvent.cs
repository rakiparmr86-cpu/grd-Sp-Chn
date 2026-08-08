namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record InventoryReservedIntegrationEvent(Guid ReservationId, Guid OrderId, Guid ProductId, decimal Quantity) : IntegrationEvent;
