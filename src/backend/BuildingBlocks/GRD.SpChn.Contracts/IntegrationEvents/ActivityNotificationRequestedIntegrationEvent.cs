namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record ActivityNotificationRequestedIntegrationEvent(
    string ActivityCode,
    string ReferenceType,
    Guid ReferenceId,
    string Subject,
    string Body,
    IReadOnlyCollection<Guid> RecipientUserIds,
    IReadOnlyCollection<string> RecipientPermissionCodes) : IntegrationEvent;
