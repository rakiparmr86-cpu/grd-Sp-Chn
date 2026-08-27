using GRD.SpChn.Identity.Application.AccessProfiles.GetAccessProfiles;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Identity.Application.AccessProfiles.ReplacePermissions;

public sealed record ReplaceAccessProfilePermissionsCommand(
    string AccessProfileCode,
    IReadOnlyCollection<string> PermissionCodes)
    : IRequest<Result<AccessProfileDetailsResponse>>;
