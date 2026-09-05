using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.ReceivePurchasedStock;

public sealed record ReceivePurchasedStockCommand(
    Guid EventId,
    Guid QualityInspectionId,
    Guid GoodsReceiptId,
    Guid DestinationOrganizationUnitId,
    IReadOnlyCollection<ReceivePurchasedStockItem> Items)
    : IRequest<Result<ReceivePurchasedStockResponse>>, ITransactionalRequest;

public sealed record ReceivePurchasedStockItem(Guid ProductId, decimal Quantity);
public sealed record ReceivePurchasedStockResponse(Guid GoodsReceiptId, bool WasDuplicate);
