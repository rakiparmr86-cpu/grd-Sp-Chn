using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.ReserveStock;

public sealed record ReserveStockCommand(
    Guid EventId,
    Guid OrderId,
    IReadOnlyCollection<ReserveStockItem> Items)
    : IRequest<Result<ReserveStockResponse>>, ITransactionalRequest;

public sealed record ReserveStockItem(Guid ProductId, decimal Quantity);

public sealed record ReserveStockResponse(
    Guid OrderId,
    ReservationOutcome Outcome,
    string? FailureReason = null);

public enum ReservationOutcome
{
    Reserved,
    Rejected,
    Duplicate
}
