using GRD.SpChn.Procurement.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Procurement.Application.PurchaseOrders;

public sealed record ListPurchaseOrdersQuery(
    Guid OrganizationUnitId,
    bool IncludeAllOrganizationUnits)
    : IRequest<IReadOnlyCollection<PurchaseOrderResponse>>;

internal sealed class ListPurchaseOrdersQueryHandler(IProcurementRepository repository)
    : IRequestHandler<ListPurchaseOrdersQuery, IReadOnlyCollection<PurchaseOrderResponse>>
{
    public async Task<IReadOnlyCollection<PurchaseOrderResponse>> Handle(
        ListPurchaseOrdersQuery request,
        CancellationToken cancellationToken) =>
        (await repository.ListPurchaseOrdersAsync(
            request.OrganizationUnitId,
            request.IncludeAllOrganizationUnits,
            cancellationToken))
        .Select(PurchaseOrderResponse.From)
        .ToArray();
}
