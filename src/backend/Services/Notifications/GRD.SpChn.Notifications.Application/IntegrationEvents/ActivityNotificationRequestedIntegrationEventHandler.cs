using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.Notifications.Application.Abstractions;

namespace GRD.SpChn.Notifications.Application.IntegrationEvents;

public sealed class ActivityNotificationRequestedIntegrationEventHandler(
    IEmailNotificationQueue emailQueue)
    : IIntegrationEventHandler<ActivityNotificationRequestedIntegrationEvent>
{
    public Task HandleAsync(
        ActivityNotificationRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        emailQueue.QueueAsync(integrationEvent, cancellationToken);
}
