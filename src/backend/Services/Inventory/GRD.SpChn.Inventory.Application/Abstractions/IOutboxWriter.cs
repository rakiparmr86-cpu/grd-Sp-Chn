using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.Inventory.Application.Abstractions;

public interface IOutboxWriter
{
    Task AddAsync(
        IIntegrationEvent integrationEvent,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default);
}
