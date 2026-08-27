using Dapper;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Infrastructure.Persistence;

namespace GRD.SpChn.Warehouse.Infrastructure.Inbox;

internal sealed class WarehouseInboxStore(WarehouseUnitOfWork unitOfWork) : IWarehouseInboxStore
{
    public async Task<bool> TryAddAsync(
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT IGNORE INTO warehouse_inbox (event_id, event_type, processed_on_utc)
            VALUES (@EventId, @EventType, @ProcessedOnUtc);
            """,
            new { EventId = eventId, EventType = eventType, ProcessedOnUtc = DateTime.UtcNow },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        return rows == 1;
    }
}
