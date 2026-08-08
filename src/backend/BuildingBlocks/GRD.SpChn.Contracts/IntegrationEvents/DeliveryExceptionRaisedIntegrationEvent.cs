namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record DeliveryExceptionRaisedIntegrationEvent(Guid DeliveryId, Guid ShipmentId, string ExceptionCode) : IntegrationEvent;
