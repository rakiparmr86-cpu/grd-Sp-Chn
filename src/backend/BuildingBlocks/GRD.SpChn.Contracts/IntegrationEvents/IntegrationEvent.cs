namespace GRD.SpChn.Contracts.IntegrationEvents;

public abstract record IntegrationEvent : IIntegrationEvent
{
    public const int InitialSchemaVersion = 1;

    public int SchemaVersion { get; init; } = InitialSchemaVersion;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
