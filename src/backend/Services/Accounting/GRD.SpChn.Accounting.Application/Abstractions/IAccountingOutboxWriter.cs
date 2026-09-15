using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.Accounting.Application.Abstractions;

public interface IAccountingOutboxWriter
{
    Task AddAsync(
        IIntegrationEvent integrationEvent,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default);
}
