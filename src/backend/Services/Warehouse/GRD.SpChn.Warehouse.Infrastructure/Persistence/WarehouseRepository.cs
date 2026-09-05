using System.Data.Common;
using Dapper;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Domain;

namespace GRD.SpChn.Warehouse.Infrastructure.Persistence;

internal sealed class WarehouseRepository(
    IDbConnectionFactory connectionFactory,
    WarehouseUnitOfWork unitOfWork) : IWarehouseRepository
{
    public async Task AddExpectedPurchaseOrderAsync(
        ExpectedPurchaseOrder order,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO warehouse_expected_purchase_orders
                (purchase_order_id, purchase_order_number, supplier_id,
                 destination_organization_unit_id, status, issued_on_utc, updated_on_utc)
            VALUES
                (@PurchaseOrderId, @PurchaseOrderNumber, @SupplierId,
                 @DestinationOrganizationUnitId, @Status, @IssuedOnUtc, @UpdatedOnUtc);
            """,
            Parameters(order),
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        foreach (var item in order.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO warehouse_expected_purchase_order_items
                    (purchase_order_id, product_id, quantity, unit_of_measure)
                VALUES (@PurchaseOrderId, @ProductId, @Quantity, @UnitOfMeasure);
                """,
                new { order.PurchaseOrderId, item.ProductId, item.Quantity, item.UnitOfMeasure },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<ExpectedPurchaseOrder?> GetExpectedPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await LoadAsync(connection, null, purchaseOrderId, false, cancellationToken);
    }

    public Task<ExpectedPurchaseOrder?> GetExpectedPurchaseOrderForUpdateAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            unitOfWork.Connection,
            unitOfWork.Transaction,
            purchaseOrderId,
            true,
            cancellationToken);

    public async Task UpdateExpectedPurchaseOrderAsync(
        ExpectedPurchaseOrder order,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE warehouse_expected_purchase_orders
            SET status = @Status,
                updated_on_utc = @UpdatedOnUtc
            WHERE purchase_order_id = @PurchaseOrderId;
            """,
            Parameters(order),
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        if (rows != 1) throw new InvalidOperationException($"Expected PO '{order.PurchaseOrderId}' could not be updated.");
    }

    public async Task AddGoodsReceiptAsync(
        GoodsReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO warehouse_goods_receipts
                (id, goods_receipt_number, purchase_order_id,
                 destination_organization_unit_id, received_by_user_id, received_on_utc)
            VALUES
                (@Id, @GoodsReceiptNumber, @PurchaseOrderId,
                 @DestinationOrganizationUnitId, @ReceivedByUserId, @ReceivedOnUtc);
            """,
            receipt,
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        foreach (var item in receipt.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO warehouse_goods_receipt_items
                    (goods_receipt_id, product_id, quantity, unit_of_measure)
                VALUES (@GoodsReceiptId, @ProductId, @Quantity, @UnitOfMeasure);
                """,
                new { GoodsReceiptId = receipt.Id, item.ProductId, item.Quantity, item.UnitOfMeasure },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<GoodsReceipt?> GetGoodsReceiptByPurchaseOrderAsync(
        Guid purchaseOrderId,
        bool forUpdate = false,
        CancellationToken cancellationToken = default)
    {
        if (forUpdate)
        {
            return await LoadGoodsReceiptAsync(
                unitOfWork.Connection,
                unitOfWork.Transaction,
                purchaseOrderId,
                true,
                cancellationToken);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await LoadGoodsReceiptAsync(connection, null, purchaseOrderId, false, cancellationToken);
    }

    public async Task<QualityInspection?> GetQualityInspectionByPurchaseOrderAsync(
        Guid purchaseOrderId,
        bool forUpdate = false,
        CancellationToken cancellationToken = default)
    {
        var connection = forUpdate
            ? unitOfWork.Connection
            : await connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<QualityInspectionRow>(new CommandDefinition(
                """
                SELECT id AS Id,
                       goods_receipt_id AS GoodsReceiptId,
                       purchase_order_id AS PurchaseOrderId,
                       destination_organization_unit_id AS DestinationOrganizationUnitId,
                       inspected_by_user_id AS InspectedByUserId,
                       result AS Result,
                       notes AS Notes,
                       inspected_on_utc AS InspectedOnUtc
                FROM warehouse_quality_inspections
                WHERE purchase_order_id = @PurchaseOrderId
                """ + (forUpdate ? " FOR UPDATE;" : ";"),
                new { PurchaseOrderId = purchaseOrderId },
                forUpdate ? unitOfWork.Transaction : null,
                cancellationToken: cancellationToken));
            return row is null
                ? null
                : QualityInspection.Rehydrate(
                    row.Id,
                    row.GoodsReceiptId,
                    row.PurchaseOrderId,
                    row.DestinationOrganizationUnitId,
                    row.InspectedByUserId,
                    Enum.Parse<QualityInspectionResult>(row.Result, true),
                    row.Notes,
                    DateTime.SpecifyKind(row.InspectedOnUtc, DateTimeKind.Utc));
        }
        finally
        {
            if (!forUpdate) await connection.DisposeAsync();
        }
    }

    public Task AddQualityInspectionAsync(
        QualityInspection inspection,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO warehouse_quality_inspections
                (id, goods_receipt_id, purchase_order_id,
                 destination_organization_unit_id, inspected_by_user_id,
                 result, notes, inspected_on_utc)
            VALUES
                (@Id, @GoodsReceiptId, @PurchaseOrderId,
                 @DestinationOrganizationUnitId, @InspectedByUserId,
                 @Result, @Notes, @InspectedOnUtc);
            """,
            new
            {
                inspection.Id,
                inspection.GoodsReceiptId,
                inspection.PurchaseOrderId,
                inspection.DestinationOrganizationUnitId,
                inspection.InspectedByUserId,
                Result = inspection.Result.ToString(),
                inspection.Notes,
                inspection.InspectedOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));

    private static async Task<GoodsReceipt?> LoadGoodsReceiptAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid purchaseOrderId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<GoodsReceiptRow>(new CommandDefinition(
            """
            SELECT id AS Id,
                   goods_receipt_number AS GoodsReceiptNumber,
                   purchase_order_id AS PurchaseOrderId,
                   destination_organization_unit_id AS DestinationOrganizationUnitId,
                   received_by_user_id AS ReceivedByUserId,
                   received_on_utc AS ReceivedOnUtc
            FROM warehouse_goods_receipts
            WHERE purchase_order_id = @PurchaseOrderId
            """ + (forUpdate ? " FOR UPDATE;" : ";"),
            new { PurchaseOrderId = purchaseOrderId },
            transaction,
            cancellationToken: cancellationToken));
        if (row is null) return null;

        var items = (await connection.QueryAsync<ItemRow>(new CommandDefinition(
            """
            SELECT product_id AS ProductId,
                   quantity AS Quantity,
                   unit_of_measure AS UnitOfMeasure
            FROM warehouse_goods_receipt_items
            WHERE goods_receipt_id = @GoodsReceiptId
            ORDER BY product_id;
            """,
            new { GoodsReceiptId = row.Id },
            transaction,
            cancellationToken: cancellationToken)))
            .Select(item => new ReceivedItem(item.ProductId, item.Quantity, item.UnitOfMeasure))
            .ToArray();
        return GoodsReceipt.Rehydrate(
            row.Id,
            row.GoodsReceiptNumber,
            row.PurchaseOrderId,
            row.DestinationOrganizationUnitId,
            row.ReceivedByUserId,
            items,
            DateTime.SpecifyKind(row.ReceivedOnUtc, DateTimeKind.Utc));
    }

    private static async Task<ExpectedPurchaseOrder?> LoadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid purchaseOrderId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition(
            SelectSql + " WHERE purchase_order_id = @PurchaseOrderId" +
            (forUpdate ? " FOR UPDATE;" : ";"),
            new { PurchaseOrderId = purchaseOrderId },
            transaction,
            cancellationToken: cancellationToken));
        if (row is null) return null;
        var items = (await connection.QueryAsync<ItemRow>(new CommandDefinition(
            """
            SELECT product_id AS ProductId, quantity AS Quantity, unit_of_measure AS UnitOfMeasure
            FROM warehouse_expected_purchase_order_items
            WHERE purchase_order_id = @PurchaseOrderId
            ORDER BY product_id;
            """,
            new { PurchaseOrderId = purchaseOrderId },
            transaction,
            cancellationToken: cancellationToken)))
            .Select(item => new ExpectedPurchaseOrderItem(
                item.ProductId, item.Quantity, item.UnitOfMeasure))
            .ToArray();
        return ExpectedPurchaseOrder.Rehydrate(
            row.PurchaseOrderId,
            row.PurchaseOrderNumber,
            row.SupplierId,
            row.DestinationOrganizationUnitId,
            Enum.Parse<ExpectedPurchaseOrderStatus>(row.Status, true),
            items,
            DateTime.SpecifyKind(row.IssuedOnUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(row.UpdatedOnUtc, DateTimeKind.Utc));
    }

    private static object Parameters(ExpectedPurchaseOrder order) => new
    {
        order.PurchaseOrderId,
        order.PurchaseOrderNumber,
        order.SupplierId,
        order.DestinationOrganizationUnitId,
        Status = order.Status.ToString(),
        order.IssuedOnUtc,
        order.UpdatedOnUtc
    };

    private const string SelectSql = """
        SELECT purchase_order_id AS PurchaseOrderId,
               purchase_order_number AS PurchaseOrderNumber,
               supplier_id AS SupplierId,
               destination_organization_unit_id AS DestinationOrganizationUnitId,
               status AS Status,
               issued_on_utc AS IssuedOnUtc,
               updated_on_utc AS UpdatedOnUtc
        FROM warehouse_expected_purchase_orders
        """;

    private sealed record OrderRow(
        Guid PurchaseOrderId,
        string PurchaseOrderNumber,
        Guid SupplierId,
        Guid DestinationOrganizationUnitId,
        string Status,
        DateTime IssuedOnUtc,
        DateTime UpdatedOnUtc);
    private sealed record ItemRow(Guid ProductId, decimal Quantity, string UnitOfMeasure);
    private sealed record GoodsReceiptRow(
        Guid Id,
        string GoodsReceiptNumber,
        Guid PurchaseOrderId,
        Guid DestinationOrganizationUnitId,
        Guid ReceivedByUserId,
        DateTime ReceivedOnUtc);
    private sealed record QualityInspectionRow(
        Guid Id,
        Guid GoodsReceiptId,
        Guid PurchaseOrderId,
        Guid DestinationOrganizationUnitId,
        Guid InspectedByUserId,
        string Result,
        string? Notes,
        DateTime InspectedOnUtc);
}
