using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.SetStock;

public sealed record SetStockCommand(
    Guid ProductId,
    decimal AvailableQuantity)
    : IRequest<Result<StockResponse>>, ITransactionalRequest;
