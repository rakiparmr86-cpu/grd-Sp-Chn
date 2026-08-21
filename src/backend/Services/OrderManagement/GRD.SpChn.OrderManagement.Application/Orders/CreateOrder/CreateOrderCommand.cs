using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyCollection<CreateOrderItem> Items)
    : IRequest<Result<OrderResponse>>, ITransactionalRequest;

public sealed record CreateOrderItem(Guid ProductId, decimal Quantity);
