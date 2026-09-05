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
    IWarehouseOutboxWriter outboxWriter)
    : IRequestHandler<CompleteQualityInspectionCommand, Result<QualityInspectionResponse>>
{
    public async Task<Result<QualityInspectionResponse>> Handle(
        CompleteQualityInspectionCommand request,
        CancellationToken cancellationToken)
    {
        var receipt = await repository.GetGoodsReceiptByPurchaseOrderAsync(
            request.PurchaseOrderId,
            forUpdate: true,
            cancellationToken);
        if (receipt is null)
        {
            return Result<QualityInspectionResponse>.Failure(Error.NotFound(
                "Warehouse.GoodsReceiptNotFound",
                $"Post the goods receipt for purchase order '{request.PurchaseOrderId}' before quality inspection."));
        }

        var existing = await repository.GetQualityInspectionByPurchaseOrderAsync(
            request.PurchaseOrderId,
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
                await outboxWriter.AddAsync(
                    new QualityInspectionApprovedIntegrationEvent(
                        inspection.Id,
                        receipt.Id,
                        receipt.GoodsReceiptNumber,
                        receipt.PurchaseOrderId,
                        receipt.DestinationOrganizationUnitId,
                        inspection.InspectedByUserId,
                        receipt.Items.Select(item => new QualityApprovedItem(
                            item.ProductId,
                            item.Quantity,
                            item.UnitOfMeasure)).ToArray())
                    {
                        OccurredOnUtc = inspection.InspectedOnUtc
                    },
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
