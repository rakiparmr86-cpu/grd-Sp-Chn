using GRD.SpChn.Warehouse.Domain;

namespace GRD.SpChn.Warehouse.Application.Abstractions;

public interface IWarehouseRepository
{
    Task AddExpectedPurchaseOrderAsync(ExpectedPurchaseOrder order, CancellationToken cancellationToken = default);
    Task<ExpectedPurchaseOrder?> GetExpectedPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<ExpectedPurchaseOrder?> GetExpectedPurchaseOrderForUpdateAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task UpdateExpectedPurchaseOrderAsync(ExpectedPurchaseOrder order, CancellationToken cancellationToken = default);
    Task AddGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default);
}
