using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Inventory.Application.Abstractions;

namespace GRD.SpChn.Inventory.Application.IntegrationEvents;

public sealed class OrderPlacedIntegrationEventHandler(
    IUnitOfWork unitOfWork,
    IInboxStore inboxStore,
    IInventoryRepository repository,
    IOutboxWriter outboxWriter)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task HandleAsync(
        OrderPlacedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var isNewMessage = await inboxStore.TryAddAsync(
                    integrationEvent.EventId,
                    nameof(OrderPlacedIntegrationEvent),
                    transactionCancellationToken);
                if (!isNewMessage)
                {
                    return false;
                }

                string? failureReason = null;
                var reservations = new List<(Domain.StockItem Stock, decimal Quantity)>();

                foreach (var requestedItem in integrationEvent.Items)
                {
                    var stock = await repository.GetByProductIdForUpdateAsync(
                        requestedItem.ProductId,
                        transactionCancellationToken);
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
                if (failureReason is null)
                {
                    foreach (var reservation in reservations)
                    {
                        reservation.Stock.Reserve(reservation.Quantity);
                        await repository.UpdateAsync(
                            reservation.Stock,
                            transactionCancellationToken);
                    }

                    resultEvent = new StockReservedIntegrationEvent(
                        Guid.NewGuid(),
                        integrationEvent.OrderId,
                        integrationEvent.Items
                            .Select(item =>
                                new StockReservedItem(item.ProductId, item.Quantity))
                            .ToArray());
                    routingKey = MessagingTopology.StockReservedRoutingKey;
                }
                else
                {
                    resultEvent = new StockReservationFailedIntegrationEvent(
                        integrationEvent.OrderId,
                        failureReason);
                    routingKey = MessagingTopology.StockReservationFailedRoutingKey;
                }

                await outboxWriter.AddAsync(
                    resultEvent,
                    MessagingTopology.InventoryExchange,
                    routingKey,
                    transactionCancellationToken);
                return true;
            },
            cancellationToken);
    }
}
