namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record StockReservedIntegrationEvent(
    Guid ReservationId,
    Guid OrderId,
    IReadOnlyCollection<StockReservedItem> Items) : IntegrationEvent;

public sealed record StockReservedItem(Guid ProductId, decimal Quantity);
