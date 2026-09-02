using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Contracts.IntegrationEvents;
using MediatR;

namespace GRD.SpChn.Procurement.Application.MaterialRequests;

public sealed record ApproveMaterialRequestCommand(Guid RequestId, Guid ApprovedByUserId)
    : IRequest<Result<MaterialRequestResponse>>, ITransactionalRequest;

internal sealed class ApproveMaterialRequestCommandHandler(
    IProcurementRepository repository,
    IOutboxWriter outboxWriter)
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
            await outboxWriter.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    "procurement.material-request.approved",
                    "MaterialRequest",
                    materialRequest.Id,
                    $"Material requisition {materialRequest.RequestNumber} approved",
                    $"{materialRequest.RequestNumber} was approved and is ready for purchase-order creation.",
                    [materialRequest.RequestedByUserId],
                    [])
                {
                    OccurredOnUtc = materialRequest.UpdatedOnUtc
                },
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);
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
