using GRD.SpChn.Identity.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Identity.Application.AccessProfiles.GetPermissionCatalog;

internal sealed class GetPermissionCatalogQueryHandler(IAccessProfileRepository repository)
    : IRequestHandler<GetPermissionCatalogQuery, IReadOnlyCollection<PermissionDefinitionResponse>>
{
    public async Task<IReadOnlyCollection<PermissionDefinitionResponse>> Handle(
        GetPermissionCatalogQuery request,
        CancellationToken cancellationToken) =>
        (await repository.GetPermissionCatalogAsync(cancellationToken))
            .Select(permission => new PermissionDefinitionResponse(
                permission.Code,
                permission.DisplayName,
                permission.Module,
                permission.Description,
                permission.IsActive))
            .ToArray();
}
