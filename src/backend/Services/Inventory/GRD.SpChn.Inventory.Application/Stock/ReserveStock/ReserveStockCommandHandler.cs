using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Stock.ReserveStock;

internal sealed class ReserveStockCommandHandler(
    IInboxStore inboxStore,
    IInventoryRepository repository,
    IOutboxWriter outboxWriter)
    : IRequestHandler<ReserveStockCommand, Result<ReserveStockResponse>>
{
    public async Task<Result<ReserveStockResponse>> Handle(
        ReserveStockCommand request,
        CancellationToken cancellationToken)
    {
        var isNewMessage = await inboxStore.TryAddAsync(
            request.EventId,
            nameof(OrderPlacedIntegrationEvent),
            cancellationToken);
        if (!isNewMessage)
        {
            return Result<ReserveStockResponse>.Success(
                new ReserveStockResponse(
                    request.OrderId,
                    ReservationOutcome.Duplicate));
        }

        string? failureReason = null;
        var reservations = new List<(Domain.StockItem Stock, decimal Quantity)>();

        foreach (var requestedItem in request.Items)
        {
            var stock = await repository.GetByProductIdForUpdateAsync(
                requestedItem.ProductId,
                cancellationToken);
            if (stock is null)
            {
                failureReason =
                    $"Product {requestedItem.ProductId} has no stock record.";
                break;
            }

            if (!stock.CanReserve(requestedItem.Quantity))
            {
                failureReason =
                    $"Product {requestedItem.ProductId} has {stock.AvailableQuantity} " +
                    $"units available but {requestedItem.Quantity} were requested.";
                break;
            }

            reservations.Add((stock, requestedItem.Quantity));
        }

        IIntegrationEvent resultEvent;
        string routingKey;
        ReservationOutcome outcome;

        if (failureReason is null)
        {
            foreach (var reservation in reservations)
            {
                reservation.Stock.Reserve(reservation.Quantity);
                await repository.UpdateAsync(reservation.Stock, cancellationToken);
            }

            resultEvent = new StockReservedIntegrationEvent(
                Guid.NewGuid(),
                request.OrderId,
                request.Items
                    .Select(item => new StockReservedItem(item.ProductId, item.Quantity))
                    .ToArray());
            routingKey = MessagingTopology.StockReservedRoutingKey;
            outcome = ReservationOutcome.Reserved;
        }
        else
        {
            resultEvent = new StockReservationFailedIntegrationEvent(
                request.OrderId,
                failureReason);
            routingKey = MessagingTopology.StockReservationFailedRoutingKey;
            outcome = ReservationOutcome.Rejected;
        }

        await outboxWriter.AddAsync(
            resultEvent,
            MessagingTopology.InventoryExchange,
            routingKey,
            cancellationToken);

        return Result<ReserveStockResponse>.Success(
            new ReserveStockResponse(request.OrderId, outcome, failureReason));
    }
}
