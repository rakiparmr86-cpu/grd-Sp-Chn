using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.Warehouse.Application.Abstractions;

public interface IWarehouseOutboxWriter
{
    Task AddAsync(
        IIntegrationEvent integrationEvent,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default);
}
