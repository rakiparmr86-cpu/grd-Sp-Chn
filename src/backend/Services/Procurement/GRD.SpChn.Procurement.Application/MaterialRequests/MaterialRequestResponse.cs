using GRD.SpChn.Procurement.Domain;

namespace GRD.SpChn.Procurement.Application.MaterialRequests;

public sealed record MaterialRequestResponse(
    Guid Id,
    string RequestNumber,
    Guid RequestingOrganizationUnitId,
    Guid DestinationOrganizationUnitId,
    Guid RequestedByUserId,
    string Purpose,
    string Status,
    IReadOnlyCollection<MaterialRequestItemResponse> Items,
    Guid? ApprovedByUserId,
    Guid? PurchaseOrderId,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc)
{
    public static MaterialRequestResponse From(MaterialRequest request) =>
        new(
            request.Id,
            request.RequestNumber,
            request.RequestingOrganizationUnitId,
            request.DestinationOrganizationUnitId,
            request.RequestedByUserId,
            request.Purpose,
            request.Status.ToString(),
            request.Items.Select(item => new MaterialRequestItemResponse(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure)).ToArray(),
            request.ApprovedByUserId,
            request.PurchaseOrderId,
            request.CreatedOnUtc,
            request.UpdatedOnUtc);
}

public sealed record MaterialRequestItemResponse(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);
