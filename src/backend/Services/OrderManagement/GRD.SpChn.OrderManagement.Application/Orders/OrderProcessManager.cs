using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.Orders;

public sealed class OrderProcessManager(
    IUnitOfWork unitOfWork,
    IInboxStore inboxStore,
    IOrderRepository repository)
{
    public Task ProcessReservationResultAsync(
        Guid eventId,
        string eventType,
        Guid orderId,
        OrderStatus targetStatus,
        CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var isNewMessage = await inboxStore.TryAddAsync(
                    eventId,
                    eventType,
                    transactionCancellationToken);
                if (!isNewMessage)
                {
                    return false;
                }

                var order = await repository.GetByIdForUpdateAsync(
                    orderId,
                    transactionCancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Order '{orderId}' was not found while processing '{eventType}'.");

                if (order.Status == targetStatus)
                {
                    return true;
                }

                if (targetStatus == OrderStatus.Confirmed)
                {
                    order.Confirm();
                }
                else if (targetStatus == OrderStatus.Cancelled)
                {
                    order.Cancel();
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported reservation result status '{targetStatus}'.");
                }

                await repository.UpdateAsync(order, transactionCancellationToken);
                return true;
            },
            cancellationToken);
}
