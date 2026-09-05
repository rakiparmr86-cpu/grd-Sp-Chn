using GRD.SpChn.ProductCatalog.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.ProductCatalog.Application.Items;

public sealed record GetProcurementItemsQuery
    : IRequest<IReadOnlyCollection<CatalogItemResponse>>;

public sealed record CatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string CategoryCode,
    string CategoryName,
    string BaseUnitOfMeasure,
    string UnitOfMeasureName,
    bool InventoryTracked);

internal sealed class GetProcurementItemsQueryHandler(ICatalogRepository repository)
    : IRequestHandler<GetProcurementItemsQuery, IReadOnlyCollection<CatalogItemResponse>>
{
    public async Task<IReadOnlyCollection<CatalogItemResponse>> Handle(
        GetProcurementItemsQuery request,
        CancellationToken cancellationToken) =>
        (await repository.GetActiveProcurementItemsAsync(cancellationToken))
            .Select(item => new CatalogItemResponse(
                item.Id,
                item.Code,
                item.Name,
                item.Description,
                item.CategoryCode,
                item.CategoryName,
                item.BaseUnitOfMeasure,
                item.UnitOfMeasureName,
                item.InventoryTracked))
            .ToArray();
}
