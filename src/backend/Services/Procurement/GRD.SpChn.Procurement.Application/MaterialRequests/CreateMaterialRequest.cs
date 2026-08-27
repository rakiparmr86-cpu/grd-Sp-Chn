using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.Procurement.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Procurement.Application.MaterialRequests;

public sealed record CreateMaterialRequestCommand(
    Guid RequestingOrganizationUnitId,
    Guid DestinationOrganizationUnitId,
    Guid RequestedByUserId,
    string Purpose,
    IReadOnlyCollection<CreateMaterialRequestItem> Items)
    : IRequest<Result<MaterialRequestResponse>>, ITransactionalRequest;

public sealed record CreateMaterialRequestItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);

internal sealed class CreateMaterialRequestCommandHandler(IProcurementRepository repository)
    : IRequestHandler<CreateMaterialRequestCommand, Result<MaterialRequestResponse>>
{
    public async Task<Result<MaterialRequestResponse>> Handle(
        CreateMaterialRequestCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var materialRequest = MaterialRequest.Create(
                request.RequestingOrganizationUnitId,
                request.DestinationOrganizationUnitId,
                request.RequestedByUserId,
                request.Purpose,
                request.Items.Select(item => MaterialRequestItem.Create(
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure)));
            await repository.AddMaterialRequestAsync(materialRequest, cancellationToken);
            return Result<MaterialRequestResponse>.Success(MaterialRequestResponse.From(materialRequest));
        }
        catch (ArgumentException exception)
        {
            return Result<MaterialRequestResponse>.Failure(Error.Validation(
                "Procurement.InvalidMaterialRequest",
                exception.Message));
        }
    }
}
