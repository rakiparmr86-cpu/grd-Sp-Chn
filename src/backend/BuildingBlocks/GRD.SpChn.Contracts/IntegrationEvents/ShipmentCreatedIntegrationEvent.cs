namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record ShipmentCreatedIntegrationEvent(Guid ShipmentId, Guid OrderId, string ShipmentNumber) : IntegrationEvent;
