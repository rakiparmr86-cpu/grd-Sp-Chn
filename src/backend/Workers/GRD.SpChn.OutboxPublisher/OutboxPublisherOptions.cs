namespace GRD.SpChn.OutboxPublisher;

public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    public int PollingIntervalMilliseconds { get; init; } = 1000;
    public int BatchSize { get; init; } = 25;
    public IReadOnlyCollection<OutboxSourceOptions> Sources { get; init; } = [];
}

public sealed class OutboxSourceOptions
{
    public string Name { get; init; } = string.Empty;
    public string ConnectionStringName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
}
