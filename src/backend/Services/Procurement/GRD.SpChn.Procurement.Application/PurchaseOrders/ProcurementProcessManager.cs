using GRD.SpChn.Procurement.Application.Abstractions;

namespace GRD.SpChn.Procurement.Application.PurchaseOrders;

public sealed class ProcurementProcessManager(
    IUnitOfWork unitOfWork,
    IInboxStore inboxStore,
    IProcurementRepository repository)
{
    public Task ProcessGoodsReceiptAsync(
        Guid eventId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var isNew = await inboxStore.TryAddAsync(
                    eventId,
                    nameof(Contracts.IntegrationEvents.GoodsReceiptPostedIntegrationEvent),
                    transactionCancellationToken);
                if (!isNew) return false;

                var purchaseOrder = await repository.GetPurchaseOrderForUpdateAsync(
                    purchaseOrderId,
                    transactionCancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Purchase order '{purchaseOrderId}' was not found while processing goods receipt.");
                if (purchaseOrder.Status == Domain.PurchaseOrderStatus.Received) return true;

                var materialRequest = await repository.GetMaterialRequestForUpdateAsync(
                    purchaseOrder.MaterialRequestId,
                    transactionCancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Material request '{purchaseOrder.MaterialRequestId}' was not found.");
                purchaseOrder.MarkReceived();
                materialRequest.MarkReceived();
                await repository.UpdatePurchaseOrderAsync(purchaseOrder, transactionCancellationToken);
                await repository.UpdateMaterialRequestAsync(materialRequest, transactionCancellationToken);
                return true;
            },
            cancellationToken);
}
