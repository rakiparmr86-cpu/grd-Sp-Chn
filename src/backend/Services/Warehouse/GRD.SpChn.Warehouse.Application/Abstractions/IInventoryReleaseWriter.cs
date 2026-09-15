namespace GRD.SpChn.Warehouse.Application.Abstractions;

/// <summary>
/// Writes quality-approved material into the Inventory module using the active
/// Warehouse transaction. The merged Inventory/Warehouse host implements this
/// port so inspection and stock release are atomic.
/// </summary>
public interface IInventoryReleaseWriter
{
    Task ReleaseAsync(
        Guid releaseEventId,
        Guid qualityInspectionId,
        Guid organizationUnitId,
        IReadOnlyCollection<InventoryReleaseItem> items,
        DateTime occurredOnUtc,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryReleaseItem(Guid ProductId, decimal Quantity);
