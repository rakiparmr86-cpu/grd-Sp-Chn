using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.OrderManagement.Application.Orders.GetOrder;

internal sealed class GetOrderQueryHandler(IOrderRepository repository)
    : IRequestHandler<GetOrderQuery, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(
        GetOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        return order is null
            ? Result<OrderResponse>.Failure(Error.NotFound(
                "Orders.NotFound",
                $"Order '{request.OrderId}' was not found."))
            : Result<OrderResponse>.Success(OrderResponse.From(order));
    }
}
