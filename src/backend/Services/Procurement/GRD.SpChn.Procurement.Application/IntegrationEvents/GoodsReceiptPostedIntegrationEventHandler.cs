using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Procurement.Application.PurchaseOrders;

namespace GRD.SpChn.Procurement.Application.IntegrationEvents;

public sealed class GoodsReceiptPostedIntegrationEventHandler(
    ProcurementProcessManager processManager)
    : IIntegrationEventHandler<GoodsReceiptPostedIntegrationEvent>
{
    public Task HandleAsync(
        GoodsReceiptPostedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        processManager.ProcessGoodsReceiptAsync(
            integrationEvent.EventId,
            integrationEvent.PurchaseOrderId,
            cancellationToken);
}
