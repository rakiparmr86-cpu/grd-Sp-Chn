using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Procurement.Application.Abstractions;
using GRD.SpChn.Procurement.Domain;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Procurement.Application.PurchaseOrders;

public sealed record IssuePurchaseOrderCommand(
    Guid MaterialRequestId,
    Guid SupplierId,
    string Currency,
    IReadOnlyCollection<PurchaseOrderPrice> Prices)
    : IRequest<Result<PurchaseOrderResponse>>, ITransactionalRequest;

public sealed record PurchaseOrderPrice(Guid ProductId, decimal UnitPrice);

internal sealed class IssuePurchaseOrderCommandHandler(
    IProcurementRepository repository,
    IOutboxWriter outboxWriter)
    : IRequestHandler<IssuePurchaseOrderCommand, Result<PurchaseOrderResponse>>
{
    public async Task<Result<PurchaseOrderResponse>> Handle(
        IssuePurchaseOrderCommand request,
        CancellationToken cancellationToken)
    {
        var materialRequest = await repository.GetMaterialRequestForUpdateAsync(
            request.MaterialRequestId,
            cancellationToken);
        if (materialRequest is null)
        {
            return Result<PurchaseOrderResponse>.Failure(Error.NotFound(
                "Procurement.MaterialRequestNotFound",
                $"Material request '{request.MaterialRequestId}' was not found."));
        }

        try
        {
            var prices = request.Prices.ToDictionary(item => item.ProductId, item => item.UnitPrice);
            var purchaseOrder = PurchaseOrder.Issue(
                materialRequest,
                request.SupplierId,
                request.Currency,
                prices);
            materialRequest.AttachPurchaseOrder(purchaseOrder.Id);

            await repository.AddPurchaseOrderAsync(purchaseOrder, cancellationToken);
            await repository.UpdateMaterialRequestAsync(materialRequest, cancellationToken);

            var integrationEvent = new PurchaseOrderIssuedIntegrationEvent(
                purchaseOrder.Id,
                purchaseOrder.PurchaseOrderNumber,
                purchaseOrder.MaterialRequestId,
                purchaseOrder.SupplierId,
                purchaseOrder.DestinationOrganizationUnitId,
                purchaseOrder.Currency,
                purchaseOrder.Items.Select(item => new PurchaseOrderIssuedItem(
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure,
                    item.UnitPrice)).ToArray())
            {
                OccurredOnUtc = purchaseOrder.IssuedOnUtc
            };
            await outboxWriter.AddAsync(
                integrationEvent,
                MessagingTopology.ProcurementExchange,
                MessagingTopology.PurchaseOrderIssuedRoutingKey,
                cancellationToken);
            await outboxWriter.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    "procurement.purchase-order.issued",
                    "MaterialRequest",
                    materialRequest.Id,
                    $"Purchase order {purchaseOrder.PurchaseOrderNumber} created",
                    $"A purchase order was created for requisition {materialRequest.RequestNumber}.",
                    [materialRequest.RequestedByUserId],
                    ["procurement.purchase-order.read"])
                {
                    OccurredOnUtc = purchaseOrder.IssuedOnUtc
                },
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);

            return Result<PurchaseOrderResponse>.Success(PurchaseOrderResponse.From(purchaseOrder));
        }
        catch (ArgumentException exception)
        {
            return Result<PurchaseOrderResponse>.Failure(Error.Validation(
                "Procurement.InvalidPurchaseOrder",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Result<PurchaseOrderResponse>.Failure(Error.Conflict(
                "Procurement.InvalidRequestState",
                exception.Message));
        }
    }
}
