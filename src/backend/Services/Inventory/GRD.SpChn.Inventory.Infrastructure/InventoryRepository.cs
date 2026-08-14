using System.Text.Json;
using Dapper;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Inventory.Domain;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Inventory.Infrastructure;

internal sealed class InventoryRepository(IDbConnectionFactory connectionFactory)
    : IInventoryRepository
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<ReservationProcessingResult> ReserveForOrderAsync(
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var inboxRows = await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT IGNORE INTO inventory_inbox
                    (event_id, event_type, processed_on_utc)
                VALUES
                    (@EventId, @EventType, @ProcessedOnUtc);
                """,
                new
                {
                    integrationEvent.EventId,
                    EventType = nameof(OrderPlacedIntegrationEvent),
                    ProcessedOnUtc = DateTime.UtcNow
                },
                transaction,
                cancellationToken: cancellationToken));

            if (inboxRows == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return new ReservationProcessingResult(true, false, null);
            }

            string? failureReason = null;
            var lockedStock = new List<StockItem>();
            foreach (var requestedItem in integrationEvent.Items)
            {
                var row = await connection.QuerySingleOrDefaultAsync<StockRow>(
                    new CommandDefinition(
                        """
                        SELECT product_id AS ProductId,
                               available_quantity AS AvailableQuantity
                        FROM inventory_stock
                        WHERE product_id = @ProductId
                        FOR UPDATE;
                        """,
                        new { requestedItem.ProductId },
                        transaction,
                        cancellationToken: cancellationToken));

                if (row is null)
                {
                    failureReason = $"Product {requestedItem.ProductId} has no stock record.";
                    break;
                }

                var stock = new StockItem(row.ProductId, row.AvailableQuantity);
                if (!stock.CanReserve(requestedItem.Quantity))
                {
                    failureReason =
                        $"Product {requestedItem.ProductId} has {stock.AvailableQuantity} " +
                        $"units available but {requestedItem.Quantity} were requested.";
                    break;
                }

                lockedStock.Add(stock);
            }

            IIntegrationEvent resultEvent;
            string routingKey;
            if (failureReason is null)
            {
                for (var index = 0; index < integrationEvent.Items.Count; index++)
                {
                    var requestedItem = integrationEvent.Items.ElementAt(index);
                    var stock = lockedStock[index];
                    stock.Reserve(requestedItem.Quantity);
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        UPDATE inventory_stock
                        SET available_quantity = @AvailableQuantity,
                            updated_on_utc = @UpdatedOnUtc
                        WHERE product_id = @ProductId;
                        """,
                        new
                        {
                            stock.AvailableQuantity,
                            UpdatedOnUtc = DateTime.UtcNow,
                            stock.ProductId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                resultEvent = new StockReservedIntegrationEvent(
                    Guid.NewGuid(),
                    integrationEvent.OrderId,
                    integrationEvent.Items
                        .Select(item => new StockReservedItem(item.ProductId, item.Quantity))
                        .ToArray());
                routingKey = MessagingTopology.StockReservedRoutingKey;
            }
            else
            {
                resultEvent = new StockReservationFailedIntegrationEvent(
                    integrationEvent.OrderId,
                    failureReason);
                routingKey = MessagingTopology.StockReservationFailedRoutingKey;
            }

            await AddOutboxMessageAsync(
                connection,
                transaction,
                resultEvent,
                routingKey,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ReservationProcessingResult(
                IsDuplicate: false,
                Reserved: failureReason is null,
                FailureReason: failureReason);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<StockItem> SetAvailableQuantityAsync(
        Guid productId,
        decimal availableQuantity,
        CancellationToken cancellationToken = default)
    {
        var stock = new StockItem(productId, availableQuantity);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO inventory_stock
                (product_id, available_quantity, updated_on_utc)
            VALUES
                (@ProductId, @AvailableQuantity, @UpdatedOnUtc)
            ON DUPLICATE KEY UPDATE
                available_quantity = VALUES(available_quantity),
                updated_on_utc = VALUES(updated_on_utc);
            """,
            new
            {
                stock.ProductId,
                stock.AvailableQuantity,
                UpdatedOnUtc = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));

        return stock;
    }

    public async Task<StockItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<StockRow>(new CommandDefinition(
            """
            SELECT product_id AS ProductId,
                   available_quantity AS AvailableQuantity
            FROM inventory_stock
            WHERE product_id = @ProductId;
            """,
            new { ProductId = productId },
            cancellationToken: cancellationToken));

        return row is null ? null : new StockItem(row.ProductId, row.AvailableQuantity);
    }

    private static Task AddOutboxMessageAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        IIntegrationEvent integrationEvent,
        string routingKey,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO inventory_outbox
                (id, event_id, event_type, exchange_name, routing_key, payload,
                 occurred_on_utc, available_on_utc)
            VALUES
                (@Id, @EventId, @EventType, @ExchangeName, @RoutingKey, @Payload,
                 @OccurredOnUtc, @AvailableOnUtc);
            """,
            new
            {
                Id = Guid.NewGuid(),
                integrationEvent.EventId,
                EventType = integrationEvent.GetType().Name,
                ExchangeName = MessagingTopology.InventoryExchange,
                RoutingKey = routingKey,
                Payload = JsonSerializer.Serialize(
                    integrationEvent,
                    integrationEvent.GetType(),
                    SerializerOptions),
                integrationEvent.OccurredOnUtc,
                AvailableOnUtc = integrationEvent.OccurredOnUtc
            },
            transaction,
            cancellationToken: cancellationToken));

    private sealed record StockRow(Guid ProductId, decimal AvailableQuantity);
}
