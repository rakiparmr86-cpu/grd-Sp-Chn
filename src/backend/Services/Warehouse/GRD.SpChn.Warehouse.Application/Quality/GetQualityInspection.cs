using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Application.Receiving;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.Quality;

public sealed record GetQualityInspectionQuery(
    Guid PurchaseOrderId,
    Guid OrganizationUnitId)
    : IRequest<Result<QualityInspectionContextResponse>>;

internal sealed class GetQualityInspectionQueryHandler(IWarehouseRepository repository)
    : IRequestHandler<GetQualityInspectionQuery, Result<QualityInspectionContextResponse>>
{
    public async Task<Result<QualityInspectionContextResponse>> Handle(
        GetQualityInspectionQuery request,
        CancellationToken cancellationToken)
    {
        var receipt = await repository.GetGoodsReceiptByPurchaseOrderAsync(
            request.PurchaseOrderId,
            cancellationToken: cancellationToken);
        if (receipt is null)
        {
            return Result<QualityInspectionContextResponse>.Failure(Error.NotFound(
                "Warehouse.GoodsReceiptNotFound",
                $"No goods receipt exists for purchase order '{request.PurchaseOrderId}'."));
        }
        if (receipt.DestinationOrganizationUnitId != request.OrganizationUnitId)
        {
            return Result<QualityInspectionContextResponse>.Failure(new Error(
                "Warehouse.WrongReceivingLocation",
                "Quality inspection can only be viewed at the receiving location."));
        }

        var inspection = await repository.GetQualityInspectionByPurchaseOrderAsync(
            request.PurchaseOrderId,
            cancellationToken: cancellationToken);
        return Result<QualityInspectionContextResponse>.Success(new(
            GoodsReceiptResponse.From(receipt),
            inspection is null ? null : QualityInspectionResponse.From(inspection)));
    }
}
