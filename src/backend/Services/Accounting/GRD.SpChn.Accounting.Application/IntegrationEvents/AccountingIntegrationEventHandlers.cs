using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using MediatR;

namespace GRD.SpChn.Accounting.Application.IntegrationEvents;

public sealed class PurchaseOrderIssuedIntegrationEventHandler(ISender sender)
    : IIntegrationEventHandler<PurchaseOrderIssuedIntegrationEvent>
{
    public async Task HandleAsync(
        PurchaseOrderIssuedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new RecordPurchaseOrderForAccountingCommand(integrationEvent),
            cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(result.FirstError.Description);
    }
}

public sealed class QualityInspectionApprovedIntegrationEventHandler(ISender sender)
    : IIntegrationEventHandler<QualityInspectionApprovedIntegrationEvent>
{
    public async Task HandleAsync(
        QualityInspectionApprovedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new RecordAcceptedReceiptForAccountingCommand(integrationEvent),
            cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(result.FirstError.Description);
    }
}
