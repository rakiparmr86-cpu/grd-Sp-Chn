using GRD.SpChn.Inventory.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.GetStock;

internal sealed class GetStockQueryHandler(IInventoryRepository repository)
    : IRequestHandler<GetStockQuery, StockResponse?>
{
    public async Task<StockResponse?> Handle(
        GetStockQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await repository.GetByProductIdAsync(
            request.ProductId,
            cancellationToken);
        return stock is null ? null : StockResponse.From(stock);
    }
}
