namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record SalesOrderCreatedIntegrationEvent(Guid OrderId, string OrderNumber, Guid CustomerId) : IntegrationEvent;
