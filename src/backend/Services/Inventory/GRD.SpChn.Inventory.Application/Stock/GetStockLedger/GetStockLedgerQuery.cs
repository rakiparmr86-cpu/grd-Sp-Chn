using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.GetStockLedger;

// UserOrganizationUnitId comes from the token. LocationId, when given, must be that
// unit or one below it; the ledger then consolidates the location and its sub-units.
public sealed record GetStockLedgerQuery(
    Guid UserOrganizationUnitId,
    Guid? LocationId,
    Guid? ProductId,
    DateTime FromUtc,
    DateTime ToUtc) : IRequest<Result<StockLedgerResponse>>;

public sealed record GetStockLedgerLocationsQuery(Guid UserOrganizationUnitId)
    : IRequest<IReadOnlyList<StockLocation>>;

public sealed record StockLedgerResponse(
    Guid OrganizationUnitId,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<StockLedgerProductSummary> Products,
    IReadOnlyList<StockLedgerEntry> Entries);

public sealed record StockLedgerProductSummary(
    Guid ProductId,
    decimal OpeningQuantity,
    decimal InQuantity,
    decimal OutQuantity,
    decimal ClosingQuantity,
    decimal OnHandQuantity);

public sealed record StockLedgerEntry(
    Guid MovementId,
    DateTime OccurredOnUtc,
    Guid OrganizationUnitId,
    Guid ProductId,
    string MovementType,
    decimal InQuantity,
    decimal OutQuantity,
    decimal BalanceQuantity,
    string SourceType,
    Guid SourceId,
    string? GoodsReceiptNumber,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    Guid? SupplierId);

internal sealed class GetStockLedgerQueryHandler(IStockLedgerReader reader)
    : IRequestHandler<GetStockLedgerQuery, Result<StockLedgerResponse>>
{
    private static readonly TimeSpan MaximumRange = TimeSpan.FromDays(366);

    public async Task<Result<StockLedgerResponse>> Handle(
        GetStockLedgerQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ToUtc <= request.FromUtc)
        {
            return Result<StockLedgerResponse>.Failure(Error.Validation(
                "Inventory.InvalidLedgerRange",
                "The To date must be after the From date.",
                "toUtc"));
        }

        if (request.ToUtc - request.FromUtc > MaximumRange)
        {
            return Result<StockLedgerResponse>.Failure(Error.Validation(
                "Inventory.LedgerRangeTooLong",
                "The stock ledger can cover at most one year at a time.",
                "toUtc"));
        }

        var accessible = await reader.GetLocationTreeAsync(request.UserOrganizationUnitId, cancellationToken);
        var locationId = request.LocationId ?? request.UserOrganizationUnitId;
        if (accessible.All(location => location.Id != locationId))
        {
            return Result<StockLedgerResponse>.Failure(Error.Validation(
                StockLocationScope.OutOfScopeCode,
                "You can view the stock ledger only for your own location or a location below it.",
                "locationId"));
        }

        var scope = StockLocationScope.Subtree(accessible, locationId);
        var totalsBefore = await reader.GetTotalsBeforeAsync(
            scope, request.ProductId, request.FromUtc, cancellationToken);
        var movements = await reader.GetMovementsAsync(
            scope, request.ProductId, request.FromUtc, request.ToUtc, cancellationToken);
        var onHand = await reader.GetOnHandAsync(
            scope, request.ProductId, cancellationToken);

        return Result<StockLedgerResponse>.Success(StockLedgerBuilder.Build(
            locationId,
            request.FromUtc,
            request.ToUtc,
            totalsBefore,
            movements,
            onHand));
    }
}

internal sealed class GetStockLedgerLocationsQueryHandler(IStockLedgerReader reader)
    : IRequestHandler<GetStockLedgerLocationsQuery, IReadOnlyList<StockLocation>>
{
    public Task<IReadOnlyList<StockLocation>> Handle(
        GetStockLedgerLocationsQuery request,
        CancellationToken cancellationToken) =>
        reader.GetLocationTreeAsync(request.UserOrganizationUnitId, cancellationToken);
}

public static class StockLocationScope
{
    public const string OutOfScopeCode = "Inventory.LocationOutOfScope";

    // The location plus everything below it, taken only from the already-authorized tree.
    public static IReadOnlyCollection<Guid> Subtree(IReadOnlyCollection<StockLocation> tree, Guid rootId)
    {
        var childrenByParent = tree
            .Where(location => location.ParentId is not null)
            .ToLookup(location => location.ParentId!.Value, location => location.Id);
        var result = new HashSet<Guid> { rootId };
        var pending = new Queue<Guid>([rootId]);
        while (pending.TryDequeue(out var current))
        {
            foreach (var child in childrenByParent[current])
            {
                if (result.Add(child)) pending.Enqueue(child);
            }
        }

        return result;
    }
}

public static class StockLedgerBuilder
{
    // Movement rows always store a positive quantity; the type decides the direction.
    // Add every new movement type here, otherwise the ledger refuses to guess its sign.
    private static readonly IReadOnlyDictionary<string, int> DirectionByMovementType =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["QualityRelease"] = 1,
        };

    public static int DirectionOf(string movementType) =>
        DirectionByMovementType.TryGetValue(movementType, out var direction)
            ? direction
            : throw new InvalidOperationException(
                $"Stock movement type '{movementType}' has no ledger direction.");

    public static StockLedgerResponse Build(
        Guid organizationUnitId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyCollection<StockMovementTotal> totalsBefore,
        IReadOnlyCollection<StockMovementRecord> movements,
        IReadOnlyCollection<LocationOnHand> onHand)
    {
        var opening = totalsBefore
            .GroupBy(total => total.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(total => DirectionOf(total.MovementType) * total.Quantity));
        var balances = new Dictionary<Guid, decimal>(opening);
        var entries = new List<StockLedgerEntry>(movements.Count);

        foreach (var movement in movements
            .OrderBy(movement => movement.OccurredOnUtc)
            .ThenBy(movement => movement.MovementId))
        {
            var direction = DirectionOf(movement.MovementType);
            var balance = balances.GetValueOrDefault(movement.ProductId) + direction * movement.Quantity;
            balances[movement.ProductId] = balance;
            entries.Add(new StockLedgerEntry(
                movement.MovementId,
                movement.OccurredOnUtc,
                movement.OrganizationUnitId,
                movement.ProductId,
                movement.MovementType,
                direction > 0 ? movement.Quantity : 0,
                direction < 0 ? movement.Quantity : 0,
                balance,
                movement.SourceType,
                movement.SourceId,
                movement.GoodsReceiptNumber,
                movement.PurchaseOrderId,
                movement.PurchaseOrderNumber,
                movement.SupplierId));
        }

        var onHandByProduct = onHand.ToDictionary(item => item.ProductId, item => item.OnHandQuantity);
        var products = opening.Keys
            .Union(balances.Keys)
            .Union(onHandByProduct.Keys)
            .Select(productId =>
            {
                var productEntries = entries.Where(entry => entry.ProductId == productId).ToList();
                return new StockLedgerProductSummary(
                    productId,
                    opening.GetValueOrDefault(productId),
                    productEntries.Sum(entry => entry.InQuantity),
                    productEntries.Sum(entry => entry.OutQuantity),
                    balances.GetValueOrDefault(productId),
                    onHandByProduct.GetValueOrDefault(productId));
            })
            .OrderBy(summary => summary.ProductId)
            .ToList();

        return new StockLedgerResponse(organizationUnitId, fromUtc, toUtc, products, entries);
    }
}
