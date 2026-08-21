namespace GRD.SpChn.OrderManagement.Application.Abstractions;

public interface IInboxStore
{
    Task<bool> TryAddAsync(
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default);
}
