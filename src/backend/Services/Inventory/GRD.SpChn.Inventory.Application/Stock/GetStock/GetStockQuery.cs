using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.GetStock;

public sealed record GetStockQuery(Guid ProductId) : IRequest<StockResponse?>;
