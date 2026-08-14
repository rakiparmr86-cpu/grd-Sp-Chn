using MediatR;

namespace GRD.SpChn.OrderManagement.Application.Orders.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderResponse?>;
