using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.Notifications.Application.Abstractions;

public interface IEmailNotificationQueue
{
    Task QueueAsync(
        ActivityNotificationRequestedIntegrationEvent notification,
        CancellationToken cancellationToken = default);
}
