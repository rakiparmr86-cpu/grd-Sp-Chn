using MediatR;

namespace GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyCollection<CreateOrderItem> Items) : IRequest<OrderResponse>;

public sealed record CreateOrderItem(Guid ProductId, decimal Quantity);
