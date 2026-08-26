namespace GRD.SpChn.Contracts.IntegrationEvents;

public interface IIntegrationEvent
{
    int SchemaVersion { get; }
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}
