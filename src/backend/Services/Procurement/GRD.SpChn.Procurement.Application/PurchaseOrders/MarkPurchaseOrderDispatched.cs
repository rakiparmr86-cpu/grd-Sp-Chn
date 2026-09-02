using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Contracts.IntegrationEvents;
using MediatR;

namespace GRD.SpChn.Procurement.Application.PurchaseOrders;

public sealed record MarkPurchaseOrderDispatchedCommand(
    Guid PurchaseOrderId,
    Guid RecordedByUserId)
    : IRequest<Result<PurchaseOrderResponse>>, ITransactionalRequest;

internal sealed class MarkPurchaseOrderDispatchedCommandHandler(
    IProcurementRepository repository,
    IOutboxWriter outboxWriter)
    : IRequestHandler<MarkPurchaseOrderDispatchedCommand, Result<PurchaseOrderResponse>>
{
    public async Task<Result<PurchaseOrderResponse>> Handle(
        MarkPurchaseOrderDispatchedCommand request,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await repository.GetPurchaseOrderForUpdateAsync(
            request.PurchaseOrderId,
            cancellationToken);
        if (purchaseOrder is null)
        {
            return Result<PurchaseOrderResponse>.Failure(Error.NotFound(
                "Procurement.PurchaseOrderNotFound",
                $"Purchase order '{request.PurchaseOrderId}' was not found."));
        }

        try
        {
            purchaseOrder.MarkDispatched();
            await repository.UpdatePurchaseOrderAsync(purchaseOrder, cancellationToken);
            var materialRequest = await repository.GetMaterialRequestForUpdateAsync(
                purchaseOrder.MaterialRequestId,
                cancellationToken) ?? throw new InvalidOperationException(
                    $"Material request '{purchaseOrder.MaterialRequestId}' was not found.");
            await outboxWriter.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    "procurement.material.dispatched",
                    "MaterialRequest",
                    materialRequest.Id,
                    $"Material dispatched for {materialRequest.RequestNumber}",
                    $"Material against purchase order {purchaseOrder.PurchaseOrderNumber} has been marked as dispatched.",
                    [materialRequest.RequestedByUserId],
                    ["procurement.purchase-order.read"])
                {
                    OccurredOnUtc = purchaseOrder.DispatchedOnUtc ?? purchaseOrder.UpdatedOnUtc
                },
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);
            return Result<PurchaseOrderResponse>.Success(PurchaseOrderResponse.From(purchaseOrder));
        }
        catch (InvalidOperationException exception)
        {
            return Result<PurchaseOrderResponse>.Failure(Error.Conflict(
                "Procurement.InvalidPurchaseOrderState",
                exception.Message));
        }
    }
}
