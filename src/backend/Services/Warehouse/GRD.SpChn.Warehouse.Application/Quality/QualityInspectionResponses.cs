using GRD.SpChn.Warehouse.Application.Receiving;
using GRD.SpChn.Warehouse.Domain;

namespace GRD.SpChn.Warehouse.Application.Quality;

public sealed record QualityInspectionResponse(
    Guid Id,
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid DestinationOrganizationUnitId,
    Guid InspectedByUserId,
    string Result,
    string? Notes,
    DateTime InspectedOnUtc)
{
    public static QualityInspectionResponse From(QualityInspection inspection) =>
        new(
            inspection.Id,
            inspection.GoodsReceiptId,
            inspection.PurchaseOrderId,
            inspection.DestinationOrganizationUnitId,
            inspection.InspectedByUserId,
            inspection.Result.ToString(),
            inspection.Notes,
            inspection.InspectedOnUtc);
}

public sealed record QualityInspectionContextResponse(
    GoodsReceiptResponse GoodsReceipt,
    QualityInspectionResponse? Inspection);
