namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record ShipmentDispatchedIntegrationEvent(Guid ShipmentId, DateTime DispatchedOnUtc) : IntegrationEvent;
