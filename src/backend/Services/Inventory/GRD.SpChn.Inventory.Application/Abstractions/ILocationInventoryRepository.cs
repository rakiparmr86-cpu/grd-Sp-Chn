using GRD.SpChn.Inventory.Domain;

namespace GRD.SpChn.Inventory.Application.Abstractions;

public interface ILocationInventoryRepository
{
    Task<decimal?> GetOnHandQuantityAsync(
        Guid organizationUnitId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task ReceiveAsync(
        LocationStockReceipt receipt,
        CancellationToken cancellationToken = default);
}
