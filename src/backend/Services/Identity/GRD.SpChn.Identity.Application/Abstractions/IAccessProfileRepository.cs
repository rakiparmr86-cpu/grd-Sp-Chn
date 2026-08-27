using GRD.SpChn.Identity.Domain;

namespace GRD.SpChn.Identity.Application.Abstractions;

public interface IAccessProfileRepository
{
    Task<AccessProfile?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AccessProfile>> GetHrAssignableAsync(
        CancellationToken cancellationToken = default);
}
