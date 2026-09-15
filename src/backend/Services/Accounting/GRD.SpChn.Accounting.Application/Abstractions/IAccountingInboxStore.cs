namespace GRD.SpChn.Accounting.Application.Abstractions;

public interface IAccountingInboxStore
{
    Task<bool> TryAddAsync(
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default);
}
