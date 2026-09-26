namespace GRD.SpChn.Inventory.Application.Abstractions;

public interface IStockLedgerReader
{
    // The given unit and every active unit below it in the organization hierarchy.
    Task<IReadOnlyList<StockLocation>> GetLocationTreeAsync(
        Guid rootOrganizationUnitId,
        CancellationToken cancellationToken = default);

    // Net quantity per product and movement type recorded before fromUtc; the
    // ledger signs each type so the opening balance follows the same rules as entries.
    Task<IReadOnlyList<StockMovementTotal>> GetTotalsBeforeAsync(
        IReadOnlyCollection<Guid> organizationUnitIds,
        Guid? productId,
        DateTime fromUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementRecord>> GetMovementsAsync(
        IReadOnlyCollection<Guid> organizationUnitIds,
        Guid? productId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationOnHand>> GetOnHandAsync(
        IReadOnlyCollection<Guid> organizationUnitIds,
        Guid? productId,
        CancellationToken cancellationToken = default);
}

public sealed record StockLocation(Guid Id, Guid? ParentId, string Code, string Name);

public sealed record StockMovementTotal(
    Guid ProductId,
    string MovementType,
    decimal Quantity);

public sealed record StockMovementRecord(
    Guid MovementId,
    DateTime OccurredOnUtc,
    Guid OrganizationUnitId,
    Guid ProductId,
    string MovementType,
    decimal Quantity,
    string SourceType,
    Guid SourceId,
    string? GoodsReceiptNumber,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    Guid? SupplierId);

public sealed record LocationOnHand(Guid ProductId, decimal OnHandQuantity);
