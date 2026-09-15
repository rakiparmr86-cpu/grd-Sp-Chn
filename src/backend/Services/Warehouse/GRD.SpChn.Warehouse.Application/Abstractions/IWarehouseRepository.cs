using GRD.SpChn.Warehouse.Domain;

namespace GRD.SpChn.Warehouse.Application.Abstractions;

public interface IWarehouseRepository
{
    Task AddExpectedPurchaseOrderAsync(ExpectedPurchaseOrder order, CancellationToken cancellationToken = default);
    Task<ExpectedPurchaseOrder?> GetExpectedPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<ExpectedPurchaseOrder?> GetExpectedPurchaseOrderForUpdateAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task UpdateExpectedPurchaseOrderAsync(ExpectedPurchaseOrder order, CancellationToken cancellationToken = default);
    Task AddGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetGoodsReceiptByPurchaseOrderAsync(
        Guid purchaseOrderId,
        bool forUpdate = false,
        CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetGoodsReceiptAwaitingInspectionByPurchaseOrderAsync(
        Guid purchaseOrderId,
        bool forUpdate = false,
        CancellationToken cancellationToken = default);
    Task<QualityInspection?> GetQualityInspectionByGoodsReceiptAsync(
        Guid goodsReceiptId,
        bool forUpdate = false,
        CancellationToken cancellationToken = default);
    Task AddQualityInspectionAsync(
        QualityInspection inspection,
        CancellationToken cancellationToken = default);
}
