using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.SetStock;

public sealed record SetStockCommand(
    Guid ProductId,
    decimal AvailableQuantity) : IRequest<StockResponse>;
