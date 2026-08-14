namespace GRD.SpChn.OrderManagement.Domain;

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    IReadOnlyCollection<OrderItem> Items,
    DateTime OccurredOnUtc);
