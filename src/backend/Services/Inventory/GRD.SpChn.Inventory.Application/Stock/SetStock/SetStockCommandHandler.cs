using GRD.SpChn.Inventory.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.SetStock;

internal sealed class SetStockCommandHandler(IInventoryRepository repository)
    : IRequestHandler<SetStockCommand, StockResponse>
{
    public async Task<StockResponse> Handle(
        SetStockCommand request,
        CancellationToken cancellationToken)
    {
        var stock = await repository.SetAvailableQuantityAsync(
            request.ProductId,
            request.AvailableQuantity,
            cancellationToken);
        return StockResponse.From(stock);
    }
}
