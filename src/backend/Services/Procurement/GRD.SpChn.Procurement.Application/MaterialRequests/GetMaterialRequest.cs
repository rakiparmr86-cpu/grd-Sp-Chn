using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Procurement.Application.MaterialRequests;

public sealed record GetMaterialRequestQuery(Guid RequestId)
    : IRequest<Result<MaterialRequestResponse>>;

internal sealed class GetMaterialRequestQueryHandler(IProcurementRepository repository)
    : IRequestHandler<GetMaterialRequestQuery, Result<MaterialRequestResponse>>
{
    public async Task<Result<MaterialRequestResponse>> Handle(
        GetMaterialRequestQuery request,
        CancellationToken cancellationToken)
    {
        var materialRequest = await repository.GetMaterialRequestAsync(
            request.RequestId,
            cancellationToken);
        return materialRequest is null
            ? Result<MaterialRequestResponse>.Failure(Error.NotFound(
                "Procurement.MaterialRequestNotFound",
                $"Material request '{request.RequestId}' was not found."))
            : Result<MaterialRequestResponse>.Success(MaterialRequestResponse.From(materialRequest));
    }
}
