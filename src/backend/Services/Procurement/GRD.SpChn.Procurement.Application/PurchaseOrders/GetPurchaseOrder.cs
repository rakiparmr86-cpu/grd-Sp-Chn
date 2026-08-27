using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Procurement.Application.PurchaseOrders;

public sealed record GetPurchaseOrderQuery(Guid PurchaseOrderId)
    : IRequest<Result<PurchaseOrderResponse>>;

internal sealed class GetPurchaseOrderQueryHandler(IProcurementRepository repository)
    : IRequestHandler<GetPurchaseOrderQuery, Result<PurchaseOrderResponse>>
{
    public async Task<Result<PurchaseOrderResponse>> Handle(
        GetPurchaseOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetPurchaseOrderAsync(request.PurchaseOrderId, cancellationToken);
        return order is null
            ? Result<PurchaseOrderResponse>.Failure(Error.NotFound(
                "Procurement.PurchaseOrderNotFound",
                $"Purchase order '{request.PurchaseOrderId}' was not found."))
            : Result<PurchaseOrderResponse>.Success(PurchaseOrderResponse.From(order));
    }
}
