using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.Identity.Application.AccessProfiles.GetAccessProfiles;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Identity.Application.AccessProfiles.ReplacePermissions;

internal sealed class ReplaceAccessProfilePermissionsCommandHandler(
    IAccessProfileRepository repository)
    : IRequestHandler<ReplaceAccessProfilePermissionsCommand, Result<AccessProfileDetailsResponse>>
{
    public async Task<Result<AccessProfileDetailsResponse>> Handle(
        ReplaceAccessProfilePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetByCodeAsync(
            request.AccessProfileCode,
            cancellationToken);
        if (profile is null)
        {
            return Result<AccessProfileDetailsResponse>.Failure(Error.NotFound(
                "Identity.AccessProfileNotFound",
                "The selected access profile does not exist."));
        }

        var requestedPermissions = (request.PermissionCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var activePermissionCodes = (await repository.GetPermissionCatalogAsync(cancellationToken))
            .Where(permission => permission.IsActive)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        var unknownPermissions = requestedPermissions
            .Where(code => !activePermissionCodes.Contains(code))
            .ToArray();

        if (unknownPermissions.Length > 0)
        {
            return Result<AccessProfileDetailsResponse>.Failure(Error.Validation(
                "Identity.UnknownPermission",
                $"Unknown or inactive permission: {string.Join(", ", unknownPermissions)}.",
                nameof(request.PermissionCodes)));
        }

        if (!AccessProfilePermissionRules.PreservesRequiredAdministration(
                profile.Code,
                requestedPermissions))
        {
            return Result<AccessProfileDetailsResponse>.Failure(Error.Validation(
                "Identity.AdministratorLockout",
                "The Director profile must retain permission-management access.",
                nameof(request.PermissionCodes)));
        }

        await repository.ReplacePermissionsAsync(
            profile.Code,
            requestedPermissions,
            cancellationToken);

        return Result<AccessProfileDetailsResponse>.Success(new AccessProfileDetailsResponse(
            profile.Code,
            profile.DisplayName,
            profile.Role,
            profile.IsHrAssignable,
            profile.IsActive,
            requestedPermissions));
    }
}
