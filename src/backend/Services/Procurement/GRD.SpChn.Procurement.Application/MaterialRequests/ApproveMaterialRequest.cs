using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Procurement.Application.MaterialRequests;

public sealed record ApproveMaterialRequestCommand(Guid RequestId, Guid ApprovedByUserId)
    : IRequest<Result<MaterialRequestResponse>>, ITransactionalRequest;

internal sealed class ApproveMaterialRequestCommandHandler(IProcurementRepository repository)
    : IRequestHandler<ApproveMaterialRequestCommand, Result<MaterialRequestResponse>>
{
    public async Task<Result<MaterialRequestResponse>> Handle(
        ApproveMaterialRequestCommand request,
        CancellationToken cancellationToken)
    {
        var materialRequest = await repository.GetMaterialRequestForUpdateAsync(
            request.RequestId,
            cancellationToken);
        if (materialRequest is null)
        {
            return Result<MaterialRequestResponse>.Failure(Error.NotFound(
                "Procurement.MaterialRequestNotFound",
                $"Material request '{request.RequestId}' was not found."));
        }

        try
        {
            materialRequest.Approve(request.ApprovedByUserId);
            await repository.UpdateMaterialRequestAsync(materialRequest, cancellationToken);
            return Result<MaterialRequestResponse>.Success(MaterialRequestResponse.From(materialRequest));
        }
        catch (InvalidOperationException exception)
        {
            return Result<MaterialRequestResponse>.Failure(Error.Conflict(
                "Procurement.InvalidRequestState",
                exception.Message));
        }
    }
}
