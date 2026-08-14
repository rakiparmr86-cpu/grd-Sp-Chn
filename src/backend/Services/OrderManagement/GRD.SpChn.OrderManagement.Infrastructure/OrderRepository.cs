using System.Text.Json;
using Dapper;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Domain;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.OrderManagement.Infrastructure;

internal sealed class OrderRepository(IDbConnectionFactory connectionFactory)
    : IOrderRepository
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task AddAsync(
        Order order,
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO order_management_orders
                    (id, order_number, customer_id, status, created_on_utc, updated_on_utc)
                VALUES
                    (@Id, @OrderNumber, @CustomerId, @Status, @CreatedOnUtc, @UpdatedOnUtc);
                """,
                new
                {
                    order.Id,
                    order.OrderNumber,
                    order.CustomerId,
                    Status = order.Status.ToString(),
                    order.CreatedOnUtc,
                    order.UpdatedOnUtc
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var item in order.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO order_management_order_items
                        (order_id, product_id, quantity)
                    VALUES
                        (@OrderId, @ProductId, @Quantity);
                    """,
                    new
                    {
                        OrderId = order.Id,
                        item.ProductId,
                        item.Quantity
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO order_management_outbox
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
                    EventType = nameof(OrderPlacedIntegrationEvent),
                    ExchangeName = MessagingTopology.OrderExchange,
                    RoutingKey = MessagingTopology.OrderPlacedRoutingKey,
                    Payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions),
                    integrationEvent.OccurredOnUtc,
                    AvailableOnUtc = integrationEvent.OccurredOnUtc
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Order?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var order = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition(
            """
            SELECT id AS Id,
                   order_number AS OrderNumber,
                   customer_id AS CustomerId,
                   status AS Status,
                   created_on_utc AS CreatedOnUtc,
                   updated_on_utc AS UpdatedOnUtc
            FROM order_management_orders
            WHERE id = @OrderId;
            """,
            new { OrderId = orderId },
            cancellationToken: cancellationToken));

        if (order is null)
        {
            return null;
        }

        var items = (await connection.QueryAsync<OrderItemRow>(new CommandDefinition(
            """
            SELECT product_id AS ProductId,
                   quantity AS Quantity
            FROM order_management_order_items
            WHERE order_id = @OrderId
            ORDER BY product_id;
            """,
            new { OrderId = orderId },
            cancellationToken: cancellationToken)))
            .Select(item => OrderItem.Create(item.ProductId, item.Quantity))
            .ToArray();

        return Order.Rehydrate(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            Enum.Parse<OrderStatus>(order.Status, ignoreCase: true),
            items,
            DateTime.SpecifyKind(order.CreatedOnUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(order.UpdatedOnUtc, DateTimeKind.Utc));
    }

    public async Task<bool> ApplyReservationResultAsync(
        Guid eventId,
        string eventType,
        Guid orderId,
        OrderStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var inboxRows = await connection.ExecuteAsync(new CommandDefinition(
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
                transaction,
                cancellationToken: cancellationToken));

            if (inboxRows == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var updatedRows = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE order_management_orders
                SET status = @Status,
                    updated_on_utc = @UpdatedOnUtc
                WHERE id = @OrderId
                  AND status = 'Pending';
                """,
                new
                {
                    Status = status.ToString(),
                    UpdatedOnUtc = DateTime.UtcNow,
                    OrderId = orderId
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return updatedRows == 1;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed record OrderRow(
        Guid Id,
        string OrderNumber,
        Guid CustomerId,
        string Status,
        DateTime CreatedOnUtc,
        DateTime UpdatedOnUtc);

    private sealed record OrderItemRow(Guid ProductId, decimal Quantity);
}
