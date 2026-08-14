using GRD.SpChn.OrderManagement.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.OrderManagement.Application.Orders.GetOrder;

internal sealed class GetOrderQueryHandler(IOrderRepository repository)
    : IRequestHandler<GetOrderQuery, OrderResponse?>
{
    public async Task<OrderResponse?> Handle(
        GetOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        return order is null ? null : OrderResponse.From(order);
    }
}
