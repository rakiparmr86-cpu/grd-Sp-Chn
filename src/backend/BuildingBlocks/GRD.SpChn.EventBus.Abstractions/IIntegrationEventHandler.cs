using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.EventBus.Abstractions;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
