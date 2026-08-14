namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    IReadOnlyCollection<OrderPlacedItem> Items) : IntegrationEvent;

public sealed record OrderPlacedItem(Guid ProductId, decimal Quantity);
