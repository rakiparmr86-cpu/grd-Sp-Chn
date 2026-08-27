using MediatR;

namespace GRD.SpChn.Identity.Application.Users.GetAssignableAccessProfiles;

public sealed record GetAssignableAccessProfilesQuery
    : IRequest<IReadOnlyCollection<AccessProfileResponse>>;

public sealed record AccessProfileResponse(
    string Code,
    string DisplayName,
    string Role);
