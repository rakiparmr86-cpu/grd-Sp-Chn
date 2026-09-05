using Dapper;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Inventory.Domain;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Inventory.Infrastructure.Persistence;

internal sealed class InventoryRepository(
    IDbConnectionFactory connectionFactory,
    InventoryUnitOfWork unitOfWork) : IInventoryRepository, ILocationInventoryRepository
{
    public async Task<decimal?> GetOnHandQuantityAsync(
        Guid organizationUnitId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(
            """
            SELECT on_hand_quantity
            FROM inventory_location_stock
            WHERE organization_unit_id = @OrganizationUnitId
              AND product_id = @ProductId;
            """,
            new { OrganizationUnitId = organizationUnitId, ProductId = productId },
            cancellationToken: cancellationToken));
    }

    public async Task ReceiveAsync(
        LocationStockReceipt receipt,
        Guid eventId,
        Guid qualityInspectionId,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO inventory_stock_movements
                (id, event_id, source_type, source_id, organization_unit_id,
                 product_id, movement_type, quantity, occurred_on_utc)
            VALUES
                (@Id, @EventId, 'QualityInspection', @QualityInspectionId,
                 @OrganizationUnitId, @ProductId, 'QualityRelease', @Quantity,
                 @OccurredOnUtc);
            """,
            new
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                QualityInspectionId = qualityInspectionId,
                receipt.OrganizationUnitId,
                receipt.ProductId,
                receipt.Quantity,
                OccurredOnUtc = DateTime.UtcNow
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO inventory_location_stock
                (organization_unit_id, product_id, on_hand_quantity, updated_on_utc)
            VALUES
                (@OrganizationUnitId, @ProductId, @Quantity, @UpdatedOnUtc)
            ON DUPLICATE KEY UPDATE
                on_hand_quantity = on_hand_quantity + VALUES(on_hand_quantity),
                updated_on_utc = VALUES(updated_on_utc);
            """,
            new
            {
                receipt.OrganizationUnitId,
                receipt.ProductId,
                receipt.Quantity,
                UpdatedOnUtc = DateTime.UtcNow
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
    }

    public Task UpsertAsync(
        StockItem stock,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
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
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

    public async Task<StockItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await LoadAsync(
            connection,
            transaction: null,
            productId,
            forUpdate: false,
            cancellationToken);
    }

    public Task<StockItem?> GetByProductIdForUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            unitOfWork.Connection,
            unitOfWork.Transaction,
            productId,
            forUpdate: true,
            cancellationToken);

    public async Task UpdateAsync(
        StockItem stock,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE inventory_stock
            SET available_quantity = @AvailableQuantity,
                updated_on_utc = @UpdatedOnUtc
            WHERE product_id = @ProductId;
            """,
            new
            {
                stock.ProductId,
                stock.AvailableQuantity,
                UpdatedOnUtc = DateTime.UtcNow
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        if (rows != 1)
        {
            throw new InvalidOperationException(
                $"Stock for product '{stock.ProductId}' could not be updated.");
        }
    }

    private static async Task<StockItem?> LoadAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction? transaction,
        Guid productId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var lockClause = forUpdate ? " FOR UPDATE" : string.Empty;
        var row = await connection.QuerySingleOrDefaultAsync<StockRow>(new CommandDefinition(
            $"""
            SELECT product_id AS ProductId,
                   available_quantity AS AvailableQuantity
            FROM inventory_stock
            WHERE product_id = @ProductId{lockClause};
            """,
            new { ProductId = productId },
            transaction,
            cancellationToken: cancellationToken));

        return row is null ? null : new StockItem(row.ProductId, row.AvailableQuantity);
    }

    private sealed record StockRow(Guid ProductId, decimal AvailableQuantity);
}
