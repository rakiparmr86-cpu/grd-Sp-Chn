using Dapper;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Inventory.Infrastructure.Persistence;

/// <summary>
/// Read-only ledger over the immutable inventory_stock_movements table. Receipt and
/// PO numbers come from the Warehouse module tables, which live in the same
/// Inventory Management deployable and database.
/// </summary>
internal sealed class StockLedgerReader(IDbConnectionFactory connectionFactory) : IStockLedgerReader
{
    // Read-only use of the Organization-owned hierarchy in the shared local database.
    // Replace with an Organization API call or a replicated read model when the
    // services move to separate databases.
    public async Task<IReadOnlyList<StockLocation>> GetLocationTreeAsync(
        Guid rootOrganizationUnitId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StockLocation>(new CommandDefinition(
            """
            WITH RECURSIVE unit_tree AS (
                SELECT id, parent_id, code, name
                FROM organization_units
                WHERE id = @RootId AND is_active = TRUE
                UNION ALL
                SELECT child.id, child.parent_id, child.code, child.name
                FROM organization_units child
                INNER JOIN unit_tree parent ON child.parent_id = parent.id
                WHERE child.is_active = TRUE
            )
            SELECT id AS Id, parent_id AS ParentId, code AS Code, name AS Name
            FROM unit_tree;
            """,
            new { RootId = rootOrganizationUnitId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StockMovementTotal>> GetTotalsBeforeAsync(
        IReadOnlyCollection<Guid> organizationUnitIds,
        Guid? productId,
        DateTime fromUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StockMovementTotal>(new CommandDefinition(
            """
            SELECT product_id AS ProductId,
                   movement_type AS MovementType,
                   SUM(quantity) AS Quantity
            FROM inventory_stock_movements
            WHERE organization_unit_id IN @OrganizationUnitIds
              AND (@ProductId IS NULL OR product_id = @ProductId)
              AND occurred_on_utc < @FromUtc
            GROUP BY product_id, movement_type;
            """,
            new { OrganizationUnitIds = organizationUnitIds, ProductId = productId, FromUtc = fromUtc },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StockMovementRecord>> GetMovementsAsync(
        IReadOnlyCollection<Guid> organizationUnitIds,
        Guid? productId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StockMovementRecord>(new CommandDefinition(
            """
            SELECT m.id AS MovementId,
                   m.occurred_on_utc AS OccurredOnUtc,
                   m.organization_unit_id AS OrganizationUnitId,
                   m.product_id AS ProductId,
                   m.movement_type AS MovementType,
                   m.quantity AS Quantity,
                   m.source_type AS SourceType,
                   m.source_id AS SourceId,
                   gr.goods_receipt_number AS GoodsReceiptNumber,
                   po.purchase_order_id AS PurchaseOrderId,
                   po.purchase_order_number AS PurchaseOrderNumber,
                   po.supplier_id AS SupplierId
            FROM inventory_stock_movements m
            LEFT JOIN warehouse_quality_inspections qi
                   ON m.source_type = 'QualityInspection' AND qi.id = m.source_id
            LEFT JOIN warehouse_goods_receipts gr
                   ON gr.id = qi.goods_receipt_id
            LEFT JOIN warehouse_expected_purchase_orders po
                   ON po.purchase_order_id = qi.purchase_order_id
            WHERE m.organization_unit_id IN @OrganizationUnitIds
              AND (@ProductId IS NULL OR m.product_id = @ProductId)
              AND m.occurred_on_utc >= @FromUtc
              AND m.occurred_on_utc < @ToUtc
            ORDER BY m.occurred_on_utc, m.id;
            """,
            new
            {
                OrganizationUnitIds = organizationUnitIds,
                ProductId = productId,
                FromUtc = fromUtc,
                ToUtc = toUtc
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LocationOnHand>> GetOnHandAsync(
        IReadOnlyCollection<Guid> organizationUnitIds,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LocationOnHand>(new CommandDefinition(
            """
            SELECT product_id AS ProductId,
                   SUM(on_hand_quantity) AS OnHandQuantity
            FROM inventory_location_stock
            WHERE organization_unit_id IN @OrganizationUnitIds
              AND (@ProductId IS NULL OR product_id = @ProductId)
            GROUP BY product_id;
            """,
            new { OrganizationUnitIds = organizationUnitIds, ProductId = productId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
