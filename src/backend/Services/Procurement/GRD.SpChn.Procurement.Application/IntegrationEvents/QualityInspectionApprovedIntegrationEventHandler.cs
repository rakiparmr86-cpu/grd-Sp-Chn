using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Procurement.Application.PurchaseOrders;

namespace GRD.SpChn.Procurement.Application.IntegrationEvents;

public sealed class QualityInspectionApprovedIntegrationEventHandler(
    ProcurementProcessManager processManager)
    : IIntegrationEventHandler<QualityInspectionApprovedIntegrationEvent>
{
    public Task HandleAsync(
        QualityInspectionApprovedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        processManager.ProcessQualityApprovalAsync(
            integrationEvent.EventId,
            integrationEvent.PurchaseOrderId,
            cancellationToken);
}
