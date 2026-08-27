using Dapper;
using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.Procurement.Infrastructure.Persistence;

namespace GRD.SpChn.Procurement.Infrastructure.Inbox;

internal sealed class ProcurementInboxStore(ProcurementUnitOfWork unitOfWork) : IInboxStore
{
    public async Task<bool> TryAddAsync(
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT IGNORE INTO procurement_inbox (event_id, event_type, processed_on_utc)
            VALUES (@EventId, @EventType, @ProcessedOnUtc);
            """,
            new { EventId = eventId, EventType = eventType, ProcessedOnUtc = DateTime.UtcNow },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        return rows == 1;
    }
}
