using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Domain;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.Receiving;

public sealed record RegisterExpectedPurchaseOrderCommand(
    Guid EventId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid SupplierId,
    Guid DestinationOrganizationUnitId,
    IReadOnlyCollection<PurchaseOrderIssuedItem> Items,
    DateTime IssuedOnUtc)
    : IRequest<Result<ExpectedPurchaseOrderResponse>>, IWarehouseTransactionalRequest;

internal sealed class RegisterExpectedPurchaseOrderCommandHandler(
    IWarehouseInboxStore inboxStore,
    IWarehouseRepository repository)
    : IRequestHandler<RegisterExpectedPurchaseOrderCommand, Result<ExpectedPurchaseOrderResponse>>
{
    public async Task<Result<ExpectedPurchaseOrderResponse>> Handle(
        RegisterExpectedPurchaseOrderCommand request,
        CancellationToken cancellationToken)
    {
        var isNew = await inboxStore.TryAddAsync(
            request.EventId,
            nameof(PurchaseOrderIssuedIntegrationEvent),
            cancellationToken);
        if (!isNew)
        {
            var existing = await repository.GetExpectedPurchaseOrderForUpdateAsync(
                request.PurchaseOrderId,
                cancellationToken);
            if (existing is null)
                throw new InvalidOperationException("Warehouse Inbox exists without its expected purchase order.");
            return Result<ExpectedPurchaseOrderResponse>.Success(ExpectedPurchaseOrderResponse.From(existing));
        }

        var order = ExpectedPurchaseOrder.Register(
            request.PurchaseOrderId,
            request.PurchaseOrderNumber,
            request.SupplierId,
            request.DestinationOrganizationUnitId,
            request.Items.Select(item => new ExpectedPurchaseOrderItem(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure)),
            request.IssuedOnUtc);
        await repository.AddExpectedPurchaseOrderAsync(order, cancellationToken);
        return Result<ExpectedPurchaseOrderResponse>.Success(ExpectedPurchaseOrderResponse.From(order));
    }
}
