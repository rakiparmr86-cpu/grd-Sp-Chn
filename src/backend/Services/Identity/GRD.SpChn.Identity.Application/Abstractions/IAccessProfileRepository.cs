using GRD.SpChn.Identity.Domain;

namespace GRD.SpChn.Identity.Application.Abstractions;

public interface IAccessProfileRepository
{
    Task<AccessProfile?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AccessProfile>> GetHrAssignableAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AccessProfile>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PermissionDefinition>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken = default);

    Task ReplacePermissionsAsync(
        string accessProfileCode,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken = default);
}
