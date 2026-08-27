using MediatR;

namespace GRD.SpChn.Identity.Application.AccessProfiles.GetPermissionCatalog;

public sealed record GetPermissionCatalogQuery
    : IRequest<IReadOnlyCollection<PermissionDefinitionResponse>>;

public sealed record PermissionDefinitionResponse(
    string Code,
    string DisplayName,
    string Module,
    string Description,
    bool IsActive);
