using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.GetLocationStock;

public sealed record GetLocationStockQuery(Guid OrganizationUnitId, Guid ProductId)
    : IRequest<Result<LocationStockResponse>>;

public sealed record LocationStockResponse(
    Guid OrganizationUnitId,
    Guid ProductId,
    decimal OnHandQuantity);

internal sealed class GetLocationStockQueryHandler(ILocationInventoryRepository repository)
    : IRequestHandler<GetLocationStockQuery, Result<LocationStockResponse>>
{
    public async Task<Result<LocationStockResponse>> Handle(
        GetLocationStockQuery request,
        CancellationToken cancellationToken)
    {
        var quantity = await repository.GetOnHandQuantityAsync(
            request.OrganizationUnitId,
            request.ProductId,
            cancellationToken);
        return quantity is null
            ? Result<LocationStockResponse>.Failure(Error.NotFound(
                "Inventory.LocationStockNotFound",
                $"Product '{request.ProductId}' has no stock at location '{request.OrganizationUnitId}'."))
            : Result<LocationStockResponse>.Success(new LocationStockResponse(
                request.OrganizationUnitId,
                request.ProductId,
                quantity.Value));
    }
}
