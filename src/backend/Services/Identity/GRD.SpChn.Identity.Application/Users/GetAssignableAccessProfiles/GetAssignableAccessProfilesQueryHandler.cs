using GRD.SpChn.Identity.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Identity.Application.Users.GetAssignableAccessProfiles;

internal sealed class GetAssignableAccessProfilesQueryHandler(
    IAccessProfileRepository repository)
    : IRequestHandler<GetAssignableAccessProfilesQuery, IReadOnlyCollection<AccessProfileResponse>>
{
    public async Task<IReadOnlyCollection<AccessProfileResponse>> Handle(
        GetAssignableAccessProfilesQuery request,
        CancellationToken cancellationToken) =>
        (await repository.GetHrAssignableAsync(cancellationToken))
            .Where(profile => profile.CanBeAssignedByHr)
            .Select(profile => new AccessProfileResponse(
                profile.Code,
                profile.DisplayName,
                profile.Role))
            .ToArray();
}
