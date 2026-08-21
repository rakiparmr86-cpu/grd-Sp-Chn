using Dapper;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Infrastructure.Persistence;

namespace GRD.SpChn.OrderManagement.Infrastructure.Inbox;

internal sealed class OrderInboxStore(OrderUnitOfWork unitOfWork) : IInboxStore
{
    public async Task<bool> TryAddAsync(
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT IGNORE INTO order_management_inbox
                (event_id, event_type, processed_on_utc)
            VALUES
                (@EventId, @EventType, @ProcessedOnUtc);
            """,
            new
            {
                EventId = eventId,
                EventType = eventType,
                ProcessedOnUtc = DateTime.UtcNow
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        return rows == 1;
    }
}
