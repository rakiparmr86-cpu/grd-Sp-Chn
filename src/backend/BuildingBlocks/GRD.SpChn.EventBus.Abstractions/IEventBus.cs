using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    Task PublishRawAsync(
        string exchangeName,
        string routingKey,
        string eventType,
        Guid eventId,
        DateTime occurredOnUtc,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
