using Dapper;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Domain;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.OrderManagement.Infrastructure.Persistence;

internal sealed class OrderRepository(
    IDbConnectionFactory connectionFactory,
    OrderUnitOfWork unitOfWork) : IOrderRepository
{
    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
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
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        foreach (var item in order.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
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
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<Order?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await LoadAsync(connection, transaction: null, orderId, forUpdate: false, cancellationToken);
    }

    public Task<Order?> GetByIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            unitOfWork.Connection,
            unitOfWork.Transaction,
            orderId,
            forUpdate: true,
            cancellationToken);

    public async Task UpdateAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE order_management_orders
            SET status = @Status,
                updated_on_utc = @UpdatedOnUtc
            WHERE id = @Id;
            """,
            new
            {
                order.Id,
                Status = order.Status.ToString(),
                order.UpdatedOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        if (rows != 1)
        {
            throw new InvalidOperationException($"Order '{order.Id}' could not be updated.");
        }
    }

    private static async Task<Order?> LoadAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction? transaction,
        Guid orderId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var lockClause = forUpdate ? " FOR UPDATE" : string.Empty;
        var order = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition(
            $"""
            SELECT id AS Id,
                   order_number AS OrderNumber,
                   customer_id AS CustomerId,
                   status AS Status,
                   created_on_utc AS CreatedOnUtc,
                   updated_on_utc AS UpdatedOnUtc
            FROM order_management_orders
            WHERE id = @OrderId{lockClause};
            """,
            new { OrderId = orderId },
            transaction,
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
            transaction,
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

    private sealed record OrderRow(
        Guid Id,
        string OrderNumber,
        Guid CustomerId,
        string Status,
        DateTime CreatedOnUtc,
        DateTime UpdatedOnUtc);

    private sealed record OrderItemRow(Guid ProductId, decimal Quantity);
}
