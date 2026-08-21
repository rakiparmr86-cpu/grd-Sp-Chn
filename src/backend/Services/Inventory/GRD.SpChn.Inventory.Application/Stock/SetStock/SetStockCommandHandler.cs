using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Inventory.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.SetStock;

internal sealed class SetStockCommandHandler(IInventoryRepository repository)
    : IRequestHandler<SetStockCommand, Result<StockResponse>>
{
    public async Task<Result<StockResponse>> Handle(
        SetStockCommand request,
        CancellationToken cancellationToken)
    {
        var stock = new StockItem(request.ProductId, request.AvailableQuantity);
        await repository.UpsertAsync(stock, cancellationToken);
        return Result<StockResponse>.Success(StockResponse.From(stock));
    }
}
