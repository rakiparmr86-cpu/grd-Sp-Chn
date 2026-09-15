using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Domain;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.Quality;

public sealed record CompleteQualityInspectionCommand(
    Guid PurchaseOrderId,
    Guid InspectorOrganizationUnitId,
    Guid InspectedByUserId,
    QualityInspectionResult Result,
    string? Notes)
    : IRequest<Result<QualityInspectionResponse>>, IWarehouseTransactionalRequest;

internal sealed class CompleteQualityInspectionCommandHandler(
    IWarehouseRepository repository,
    IInventoryReleaseWriter inventoryReleaseWriter,
    IWarehouseOutboxWriter outboxWriter)
    : IRequestHandler<CompleteQualityInspectionCommand, Result<QualityInspectionResponse>>
{
    public async Task<Result<QualityInspectionResponse>> Handle(
        CompleteQualityInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var receipt = await repository.GetGoodsReceiptAwaitingInspectionByPurchaseOrderAsync(
            request.PurchaseOrderId,
            forUpdate: true,
            cancellationToken);
        if (receipt is null)
        {
            var latestReceipt = await repository.GetGoodsReceiptByPurchaseOrderAsync(
                request.PurchaseOrderId,
                forUpdate: true,
                cancellationToken);
            if (latestReceipt is not null)
            {
                var latestInspection = await repository.GetQualityInspectionByGoodsReceiptAsync(
                    latestReceipt.Id,
                    forUpdate: true,
                    cancellationToken);
                return Result<QualityInspectionResponse>.Failure(Error.Conflict(
                    "Warehouse.QualityInspectionAlreadyCompleted",
                    latestInspection is null
                        ? "No goods receipt is awaiting quality inspection."
                        : $"Quality inspection for {latestReceipt.GoodsReceiptNumber} is already {latestInspection.Result}."));
            }

            return Result<QualityInspectionResponse>.Failure(Error.NotFound(
                "Warehouse.GoodsReceiptNotFound",
                $"Post the goods receipt for purchase order '{request.PurchaseOrderId}' before quality inspection."));
        }

        var existing = await repository.GetQualityInspectionByGoodsReceiptAsync(
            receipt.Id,
            forUpdate: true,
            cancellationToken);
        if (existing is not null)
        {
            return Result<QualityInspectionResponse>.Failure(Error.Conflict(
                "Warehouse.QualityInspectionAlreadyCompleted",
                $"Quality inspection is already {existing.Result}."));
        }

        try
        {
            var inspection = QualityInspection.Complete(
                receipt,
                request.InspectorOrganizationUnitId,
                request.InspectedByUserId,
                request.Result,
                request.Notes);
            await repository.AddQualityInspectionAsync(inspection, cancellationToken);

            if (inspection.Result == QualityInspectionResult.Passed)
            {
                var approvedEvent = new QualityInspectionApprovedIntegrationEvent(
                    inspection.Id,
                    receipt.Id,
                    receipt.GoodsReceiptNumber,
                    receipt.PurchaseOrderId,
                    receipt.DestinationOrganizationUnitId,
                    inspection.InspectedByUserId,
                    receipt.Items.Select(item => new QualityApprovedItem(
                        item.ProductId,
                        item.Quantity,
                        item.UnitOfMeasure)).ToArray(),
                    receipt.CompletesPurchaseOrder)
                {
                    OccurredOnUtc = inspection.InspectedOnUtc
                };

                await inventoryReleaseWriter.ReleaseAsync(
                    approvedEvent.EventId,
                    inspection.Id,
                    receipt.DestinationOrganizationUnitId,
                    receipt.Items.Select(item => new InventoryReleaseItem(
                        item.ProductId,
                        item.Quantity)).ToArray(),
                    inspection.InspectedOnUtc,
                    cancellationToken);

                await outboxWriter.AddAsync(
                    approvedEvent,
                    MessagingTopology.WarehouseExchange,
                    MessagingTopology.QualityInspectionApprovedRoutingKey,
                    cancellationToken);
            }

            await outboxWriter.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    inspection.Result == QualityInspectionResult.Passed
                        ? "warehouse.quality-inspection.passed"
                        : "warehouse.quality-inspection.rejected",
                    "PurchaseOrder",
                    receipt.PurchaseOrderId,
                    $"Quality inspection {inspection.Result.ToString().ToLowerInvariant()} for {receipt.GoodsReceiptNumber}",
                    inspection.Result == QualityInspectionResult.Passed
                        ? "Received material passed quality inspection and was released to Inventory."
                        : $"Received material was rejected by Quality. Reason: {inspection.Notes}",
                    [],
                    ["procurement.purchase-order.read"]),
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);

            return Result<QualityInspectionResponse>.Success(QualityInspectionResponse.From(inspection));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result<QualityInspectionResponse>.Failure(new Error(
                "Warehouse.WrongReceivingLocation",
                exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Result<QualityInspectionResponse>.Failure(Error.Validation(
                "Warehouse.InvalidQualityInspection",
                exception.Message));
        }
    }
}
