using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.GetStock;

internal sealed class GetStockQueryHandler(IInventoryRepository repository)
    : IRequestHandler<GetStockQuery, Result<StockResponse>>
{
    public async Task<Result<StockResponse>> Handle(
        GetStockQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await repository.GetByProductIdAsync(
            request.ProductId,
            cancellationToken);
        return stock is null
            ? Result<StockResponse>.Failure(Error.NotFound(
                "Inventory.StockNotFound",
                $"Stock for product '{request.ProductId}' was not found."))
            : Result<StockResponse>.Success(StockResponse.From(stock));
    }
}
