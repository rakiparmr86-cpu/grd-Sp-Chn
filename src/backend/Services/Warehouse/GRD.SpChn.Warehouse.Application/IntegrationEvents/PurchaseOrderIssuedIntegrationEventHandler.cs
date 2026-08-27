using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Warehouse.Application.Receiving;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.IntegrationEvents;

public sealed class PurchaseOrderIssuedIntegrationEventHandler(ISender sender)
    : IIntegrationEventHandler<PurchaseOrderIssuedIntegrationEvent>
{
    public async Task HandleAsync(
        PurchaseOrderIssuedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new RegisterExpectedPurchaseOrderCommand(
            integrationEvent.EventId,
            integrationEvent.PurchaseOrderId,
            integrationEvent.PurchaseOrderNumber,
            integrationEvent.SupplierId,
            integrationEvent.DestinationOrganizationUnitId,
            integrationEvent.Items,
            integrationEvent.OccurredOnUtc), cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }
}
