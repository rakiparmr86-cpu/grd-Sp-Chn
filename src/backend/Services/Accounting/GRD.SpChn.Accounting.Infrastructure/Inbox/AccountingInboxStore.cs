using Dapper;
using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Infrastructure.Persistence;

namespace GRD.SpChn.Accounting.Infrastructure.Inbox;

internal sealed class AccountingInboxStore(AccountingUnitOfWork unitOfWork)
    : IAccountingInboxStore
{
    public async Task<bool> TryAddAsync(
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT IGNORE INTO accounting_inbox (event_id, event_type, processed_on_utc)
            VALUES (@EventId, @EventType, @ProcessedOnUtc);
            """,
            new { EventId = eventId, EventType = eventType, ProcessedOnUtc = DateTime.UtcNow },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        return rows == 1;
    }
}
