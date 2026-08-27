using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.Receiving;

public sealed record GetExpectedPurchaseOrderQuery(Guid PurchaseOrderId)
    : IRequest<Result<ExpectedPurchaseOrderResponse>>;

internal sealed class GetExpectedPurchaseOrderQueryHandler(IWarehouseRepository repository)
    : IRequestHandler<GetExpectedPurchaseOrderQuery, Result<ExpectedPurchaseOrderResponse>>
{
    public async Task<Result<ExpectedPurchaseOrderResponse>> Handle(
        GetExpectedPurchaseOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetExpectedPurchaseOrderAsync(
            request.PurchaseOrderId,
            cancellationToken);
        return order is null
            ? Result<ExpectedPurchaseOrderResponse>.Failure(Error.NotFound(
                "Warehouse.PurchaseOrderNotExpected",
                $"Purchase order '{request.PurchaseOrderId}' has not reached this warehouse."))
            : Result<ExpectedPurchaseOrderResponse>.Success(ExpectedPurchaseOrderResponse.From(order));
    }
}
