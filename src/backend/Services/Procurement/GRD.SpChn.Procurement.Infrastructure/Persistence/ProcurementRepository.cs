using System.Data.Common;
using Dapper;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.Procurement.Application.MaterialRequests;
using GRD.SpChn.Procurement.Domain;

namespace GRD.SpChn.Procurement.Infrastructure.Persistence;

internal sealed class ProcurementRepository(
    IDbConnectionFactory connectionFactory,
    ProcurementUnitOfWork unitOfWork) : IProcurementRepository
{
    public async Task AddMaterialRequestAsync(
        MaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO procurement_material_requests
                (id, request_number, requesting_organization_unit_id,
                 destination_organization_unit_id, requested_by_user_id, purpose,
                 status, approved_by_user_id, purchase_order_id, created_on_utc, updated_on_utc)
            VALUES
                (@Id, @RequestNumber, @RequestingOrganizationUnitId,
                 @DestinationOrganizationUnitId, @RequestedByUserId, @Purpose,
                 @Status, @ApprovedByUserId, @PurchaseOrderId, @CreatedOnUtc, @UpdatedOnUtc);
            """,
            RequestParameters(request),
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        foreach (var item in request.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO procurement_material_request_items
                    (material_request_id, product_id, quantity, unit_of_measure)
                VALUES (@RequestId, @ProductId, @Quantity, @UnitOfMeasure);
                """,
                new { RequestId = request.Id, item.ProductId, item.Quantity, item.UnitOfMeasure },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<MaterialRequest?> GetMaterialRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await LoadMaterialRequestAsync(connection, null, id, false, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MaterialRequestListItemResponse>> ListMaterialRequestsAsync(
        Guid organizationUnitId,
        bool includeAllOrganizationUnits,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MaterialRequestListRow>(new CommandDefinition(
            """
            SELECT request.id AS Id,
                   request.request_number AS RequestNumber,
                   request.purpose AS Purpose,
                   request.status AS Status,
                   COUNT(item.product_id) AS ItemCount,
                   request.requested_by_user_id AS RequestedByUserId,
                   request.created_on_utc AS CreatedOnUtc,
                   purchase_order.id AS PurchaseOrderId,
                   purchase_order.purchase_order_number AS PurchaseOrderNumber,
                   purchase_order.status AS PurchaseOrderStatus,
                   (purchase_order.id IS NOT NULL) AS PurchaseOrderCreated,
                   (purchase_order.status IN ('Dispatched', 'Received')) AS MaterialDispatched,
                   purchase_order.dispatched_on_utc AS DispatchedOnUtc
            FROM procurement_material_requests request
            LEFT JOIN procurement_material_request_items item
                   ON item.material_request_id = request.id
            LEFT JOIN procurement_purchase_orders purchase_order
                   ON purchase_order.id = request.purchase_order_id
            WHERE @IncludeAllOrganizationUnits = TRUE
               OR request.requesting_organization_unit_id = @OrganizationUnitId
            GROUP BY request.id, request.request_number, request.purpose, request.status,
                     request.requested_by_user_id, request.created_on_utc,
                     purchase_order.id, purchase_order.purchase_order_number,
                     purchase_order.status, purchase_order.dispatched_on_utc
            ORDER BY request.created_on_utc DESC, request.id DESC;
            """,
            new
            {
                OrganizationUnitId = organizationUnitId,
                IncludeAllOrganizationUnits = includeAllOrganizationUnits
            },
            cancellationToken: cancellationToken));
        return rows
            .Select(row => new MaterialRequestListItemResponse(
                row.Id,
                row.RequestNumber,
                row.Purpose,
                row.Status,
                checked((int)row.ItemCount),
                row.RequestedByUserId,
                Utc(row.CreatedOnUtc),
                row.PurchaseOrderId,
                row.PurchaseOrderNumber,
                row.PurchaseOrderStatus,
                row.PurchaseOrderCreated != 0,
                row.MaterialDispatched != 0,
                Utc(row.DispatchedOnUtc)))
            .ToArray();
    }

    public Task<MaterialRequest?> GetMaterialRequestForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        LoadMaterialRequestAsync(
            unitOfWork.Connection,
            unitOfWork.Transaction,
            id,
            true,
            cancellationToken);

    public async Task UpdateMaterialRequestAsync(
        MaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE procurement_material_requests
            SET status = @Status,
                approved_by_user_id = @ApprovedByUserId,
                purchase_order_id = @PurchaseOrderId,
                updated_on_utc = @UpdatedOnUtc
            WHERE id = @Id;
            """,
            RequestParameters(request),
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        if (rows != 1) throw new InvalidOperationException($"Material request '{request.Id}' could not be updated.");
    }

    public async Task AddPurchaseOrderAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO procurement_purchase_orders
                (id, purchase_order_number, material_request_id, supplier_id,
                 destination_organization_unit_id, currency, status, issued_on_utc,
                 dispatched_on_utc, updated_on_utc)
            VALUES
                (@Id, @PurchaseOrderNumber, @MaterialRequestId, @SupplierId,
                 @DestinationOrganizationUnitId, @Currency, @Status, @IssuedOnUtc,
                 @DispatchedOnUtc, @UpdatedOnUtc);
            """,
            PurchaseOrderParameters(purchaseOrder),
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        foreach (var item in purchaseOrder.Items)
        {
            await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO procurement_purchase_order_items
                    (purchase_order_id, product_id, quantity, unit_of_measure, unit_price)
                VALUES (@PurchaseOrderId, @ProductId, @Quantity, @UnitOfMeasure, @UnitPrice);
                """,
                new
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure,
                    item.UnitPrice
                },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<PurchaseOrder?> GetPurchaseOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await LoadPurchaseOrderAsync(connection, null, id, false, cancellationToken);
    }

    public Task<PurchaseOrder?> GetPurchaseOrderForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        LoadPurchaseOrderAsync(
            unitOfWork.Connection,
            unitOfWork.Transaction,
            id,
            true,
            cancellationToken);

    public async Task UpdatePurchaseOrderAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE procurement_purchase_orders
            SET status = @Status,
                dispatched_on_utc = @DispatchedOnUtc,
                updated_on_utc = @UpdatedOnUtc
            WHERE id = @Id;
            """,
            PurchaseOrderParameters(purchaseOrder),
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
        if (rows != 1) throw new InvalidOperationException($"Purchase order '{purchaseOrder.Id}' could not be updated.");
    }

    private static async Task<MaterialRequest?> LoadMaterialRequestAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<RequestRow>(new CommandDefinition(
            RequestSelect + " WHERE id = @Id" + (forUpdate ? " FOR UPDATE;" : ";"),
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (row is null) return null;
        var items = (await connection.QueryAsync<RequestItemRow>(new CommandDefinition(
            """
            SELECT product_id AS ProductId, quantity AS Quantity, unit_of_measure AS UnitOfMeasure
            FROM procurement_material_request_items
            WHERE material_request_id = @Id
            ORDER BY product_id;
            """,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken)))
            .Select(item => MaterialRequestItem.Create(item.ProductId, item.Quantity, item.UnitOfMeasure))
            .ToArray();
        return MaterialRequest.Rehydrate(
            row.Id, row.RequestNumber, row.RequestingOrganizationUnitId,
            row.DestinationOrganizationUnitId, row.RequestedByUserId, row.Purpose,
            Enum.Parse<MaterialRequestStatus>(row.Status, true), items,
            row.ApprovedByUserId, row.PurchaseOrderId,
            Utc(row.CreatedOnUtc), Utc(row.UpdatedOnUtc));
    }

    private static async Task<PurchaseOrder?> LoadPurchaseOrderAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<PurchaseOrderRow>(new CommandDefinition(
            PurchaseOrderSelect + " WHERE id = @Id" + (forUpdate ? " FOR UPDATE;" : ";"),
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (row is null) return null;
        var items = (await connection.QueryAsync<PurchaseOrderItemRow>(new CommandDefinition(
            """
            SELECT product_id AS ProductId, quantity AS Quantity,
                   unit_of_measure AS UnitOfMeasure, unit_price AS UnitPrice
            FROM procurement_purchase_order_items
            WHERE purchase_order_id = @Id
            ORDER BY product_id;
            """,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken)))
            .Select(item => new PurchaseOrderItem(
                item.ProductId, item.Quantity, item.UnitOfMeasure, item.UnitPrice))
            .ToArray();
        return PurchaseOrder.Rehydrate(
            row.Id, row.PurchaseOrderNumber, row.MaterialRequestId, row.SupplierId,
            row.DestinationOrganizationUnitId, row.Currency,
            Enum.Parse<PurchaseOrderStatus>(row.Status, true), items,
            Utc(row.IssuedOnUtc), Utc(row.DispatchedOnUtc), Utc(row.UpdatedOnUtc));
    }

    private static object RequestParameters(MaterialRequest request) => new
    {
        request.Id,
        request.RequestNumber,
        request.RequestingOrganizationUnitId,
        request.DestinationOrganizationUnitId,
        request.RequestedByUserId,
        request.Purpose,
        Status = request.Status.ToString(),
        request.ApprovedByUserId,
        request.PurchaseOrderId,
        request.CreatedOnUtc,
        request.UpdatedOnUtc
    };

    private static object PurchaseOrderParameters(PurchaseOrder order) => new
    {
        order.Id,
        order.PurchaseOrderNumber,
        order.MaterialRequestId,
        order.SupplierId,
        order.DestinationOrganizationUnitId,
        order.Currency,
        Status = order.Status.ToString(),
        order.IssuedOnUtc,
        order.DispatchedOnUtc,
        order.UpdatedOnUtc
    };

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value is null ? null : Utc(value.Value);

    private const string RequestSelect = """
        SELECT id AS Id, request_number AS RequestNumber,
               requesting_organization_unit_id AS RequestingOrganizationUnitId,
               destination_organization_unit_id AS DestinationOrganizationUnitId,
               requested_by_user_id AS RequestedByUserId, purpose AS Purpose,
               status AS Status, approved_by_user_id AS ApprovedByUserId,
               purchase_order_id AS PurchaseOrderId, created_on_utc AS CreatedOnUtc,
               updated_on_utc AS UpdatedOnUtc
        FROM procurement_material_requests
        """;

    private const string PurchaseOrderSelect = """
        SELECT id AS Id, purchase_order_number AS PurchaseOrderNumber,
               material_request_id AS MaterialRequestId, supplier_id AS SupplierId,
               destination_organization_unit_id AS DestinationOrganizationUnitId,
               currency AS Currency, status AS Status, issued_on_utc AS IssuedOnUtc,
               dispatched_on_utc AS DispatchedOnUtc, updated_on_utc AS UpdatedOnUtc
        FROM procurement_purchase_orders
        """;

    private sealed record RequestRow(
        Guid Id, string RequestNumber, Guid RequestingOrganizationUnitId,
        Guid DestinationOrganizationUnitId, Guid RequestedByUserId, string Purpose,
        string Status, Guid? ApprovedByUserId, Guid? PurchaseOrderId,
        DateTime CreatedOnUtc, DateTime UpdatedOnUtc);
    private sealed record RequestItemRow(Guid ProductId, decimal Quantity, string UnitOfMeasure);
    private sealed record MaterialRequestListRow(
        Guid Id, string RequestNumber, string Purpose, string Status, long ItemCount,
        Guid RequestedByUserId, DateTime CreatedOnUtc, Guid? PurchaseOrderId,
        string? PurchaseOrderNumber, string? PurchaseOrderStatus,
        int PurchaseOrderCreated, int MaterialDispatched, DateTime? DispatchedOnUtc);
    private sealed record PurchaseOrderRow(
        Guid Id, string PurchaseOrderNumber, Guid MaterialRequestId, Guid SupplierId,
        Guid DestinationOrganizationUnitId, string Currency, string Status,
        DateTime IssuedOnUtc, DateTime? DispatchedOnUtc, DateTime UpdatedOnUtc);
    private sealed record PurchaseOrderItemRow(
        Guid ProductId, decimal Quantity, string UnitOfMeasure, decimal UnitPrice);
}
