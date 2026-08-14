using GRD.SpChn.Inventory.Domain;

namespace GRD.SpChn.Inventory.Application.Stock;

public sealed record StockResponse(Guid ProductId, decimal AvailableQuantity)
{
    public static StockResponse From(StockItem stock) =>
        new(stock.ProductId, stock.AvailableQuantity);
}
