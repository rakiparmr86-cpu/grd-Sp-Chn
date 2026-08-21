using GRD.SpChn.Inventory.Domain;

namespace GRD.SpChn.Inventory.Application.Abstractions;

public interface IInventoryRepository
{
    Task UpsertAsync(StockItem stock, CancellationToken cancellationToken = default);

    Task<StockItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<StockItem?> GetByProductIdForUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(StockItem stock, CancellationToken cancellationToken = default);
}
