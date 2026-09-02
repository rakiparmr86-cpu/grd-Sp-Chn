using GRD.SpChn.Procurement.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Procurement.Application.MaterialRequests;

public sealed record ListMaterialRequestsQuery(
    Guid OrganizationUnitId,
    bool IncludeAllOrganizationUnits)
    : IRequest<IReadOnlyCollection<MaterialRequestListItemResponse>>;

public sealed record MaterialRequestListItemResponse(
    Guid Id,
    string RequestNumber,
    string Purpose,
    string Status,
    int ItemCount,
    Guid RequestedByUserId,
    DateTime CreatedOnUtc,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    string? PurchaseOrderStatus,
    bool PurchaseOrderCreated,
    bool MaterialDispatched,
    DateTime? DispatchedOnUtc);

internal sealed class ListMaterialRequestsQueryHandler(IProcurementRepository repository)
    : IRequestHandler<ListMaterialRequestsQuery, IReadOnlyCollection<MaterialRequestListItemResponse>>
{
    public Task<IReadOnlyCollection<MaterialRequestListItemResponse>> Handle(
        ListMaterialRequestsQuery request,
        CancellationToken cancellationToken) =>
        repository.ListMaterialRequestsAsync(
            request.OrganizationUnitId,
            request.IncludeAllOrganizationUnits,
            cancellationToken);
}
