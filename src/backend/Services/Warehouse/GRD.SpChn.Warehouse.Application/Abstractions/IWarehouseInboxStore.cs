namespace GRD.SpChn.Warehouse.Application.Abstractions;

public interface IWarehouseInboxStore
{
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default);
}
