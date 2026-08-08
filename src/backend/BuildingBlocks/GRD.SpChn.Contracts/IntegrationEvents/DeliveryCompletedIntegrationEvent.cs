namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record DeliveryCompletedIntegrationEvent(Guid DeliveryId, Guid ShipmentId, DateTime DeliveredOnUtc) : IntegrationEvent;
