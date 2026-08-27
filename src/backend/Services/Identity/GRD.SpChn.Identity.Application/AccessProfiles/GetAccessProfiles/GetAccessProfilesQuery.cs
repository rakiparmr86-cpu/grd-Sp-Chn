using MediatR;

namespace GRD.SpChn.Identity.Application.AccessProfiles.GetAccessProfiles;

public sealed record GetAccessProfilesQuery
    : IRequest<IReadOnlyCollection<AccessProfileDetailsResponse>>;

public sealed record AccessProfileDetailsResponse(
    string Code,
    string DisplayName,
    string Role,
    bool IsHrAssignable,
    bool IsActive,
    IReadOnlyCollection<string> Permissions);
