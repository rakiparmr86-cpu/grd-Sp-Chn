using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Domain;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.Receiving;

public sealed record PostGoodsReceiptCommand(
    Guid PurchaseOrderId,
    Guid ReceiverOrganizationUnitId,
    Guid ReceivedByUserId,
    IReadOnlyCollection<PostGoodsReceiptItem> Items)
    : IRequest<Result<GoodsReceiptResponse>>, IWarehouseTransactionalRequest;

public sealed record PostGoodsReceiptItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);

internal sealed class PostGoodsReceiptCommandHandler(
    IWarehouseRepository repository,
    IWarehouseOutboxWriter outboxWriter)
    : IRequestHandler<PostGoodsReceiptCommand, Result<GoodsReceiptResponse>>
{
    public async Task<Result<GoodsReceiptResponse>> Handle(
        PostGoodsReceiptCommand request,
        CancellationToken cancellationToken)
    {
        var expected = await repository.GetExpectedPurchaseOrderForUpdateAsync(
            request.PurchaseOrderId,
            cancellationToken);
        if (expected is null)
        {
            return Result<GoodsReceiptResponse>.Failure(Error.NotFound(
                "Warehouse.PurchaseOrderNotExpected",
                $"Purchase order '{request.PurchaseOrderId}' has not reached this warehouse."));
        }

        try
        {
            var receipt = expected.Receive(
                request.ReceiverOrganizationUnitId,
                request.ReceivedByUserId,
                request.Items.Select(item => new ReceivedItem(
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure)).ToArray());
            await repository.UpdateExpectedPurchaseOrderAsync(expected, cancellationToken);
            await repository.AddGoodsReceiptAsync(receipt, cancellationToken);

            var integrationEvent = new GoodsReceiptPostedIntegrationEvent(
                receipt.Id,
                receipt.GoodsReceiptNumber,
                receipt.PurchaseOrderId,
                receipt.DestinationOrganizationUnitId,
                receipt.ReceivedByUserId,
                receipt.Items.Select(item => new GoodsReceiptPostedItem(
                    item.ProductId,
                    item.Quantity,
                    item.UnitOfMeasure)).ToArray())
            {
                OccurredOnUtc = receipt.ReceivedOnUtc
            };
            await outboxWriter.AddAsync(
                integrationEvent,
                MessagingTopology.WarehouseExchange,
                MessagingTopology.GoodsReceiptPostedRoutingKey,
                cancellationToken);
            return Result<GoodsReceiptResponse>.Success(GoodsReceiptResponse.From(receipt));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result<GoodsReceiptResponse>.Failure(new Error(
                "Warehouse.WrongReceivingLocation",
                exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Result<GoodsReceiptResponse>.Failure(Error.Validation(
                "Warehouse.InvalidGoodsReceipt",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Result<GoodsReceiptResponse>.Failure(Error.Conflict(
                "Warehouse.InvalidReceiptState",
                exception.Message));
        }
    }
}
