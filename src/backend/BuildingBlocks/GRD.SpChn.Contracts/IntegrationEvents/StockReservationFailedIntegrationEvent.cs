namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record StockReservationFailedIntegrationEvent(
    Guid OrderId,
    string Reason) : IntegrationEvent;
