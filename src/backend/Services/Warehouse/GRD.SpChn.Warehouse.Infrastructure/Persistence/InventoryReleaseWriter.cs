using Dapper;
using GRD.SpChn.Warehouse.Application.Abstractions;

namespace GRD.SpChn.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Inventory-side persistence adapter for the merged Inventory/Warehouse
/// deployable. It deliberately uses WarehouseUnitOfWork so the quality result,
/// stock movement, stock balance and Outbox event share one database commit.
/// </summary>
internal sealed class InventoryReleaseWriter(WarehouseUnitOfWork unitOfWork)
    : IInventoryReleaseWriter
{
    public async Task ReleaseAsync(
        Guid releaseEventId,
        Guid qualityInspectionId,
        Guid organizationUnitId,
        IReadOnlyCollection<InventoryReleaseItem> items,
        DateTime occurredOnUtc,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
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
                    EventId = releaseEventId,
                    QualityInspectionId = qualityInspectionId,
                    OrganizationUnitId = organizationUnitId,
                    item.ProductId,
                    item.Quantity,
                    OccurredOnUtc = occurredOnUtc
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
                    OrganizationUnitId = organizationUnitId,
                    item.ProductId,
                    item.Quantity,
                    UpdatedOnUtc = occurredOnUtc
                },
                unitOfWork.Transaction,
                cancellationToken: cancellationToken));
        }
    }
}
