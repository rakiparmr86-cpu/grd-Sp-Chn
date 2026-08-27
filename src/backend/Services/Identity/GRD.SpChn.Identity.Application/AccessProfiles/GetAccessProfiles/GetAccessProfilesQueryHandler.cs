using GRD.SpChn.Identity.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Identity.Application.AccessProfiles.GetAccessProfiles;

internal sealed class GetAccessProfilesQueryHandler(IAccessProfileRepository repository)
    : IRequestHandler<GetAccessProfilesQuery, IReadOnlyCollection<AccessProfileDetailsResponse>>
{
    public async Task<IReadOnlyCollection<AccessProfileDetailsResponse>> Handle(
        GetAccessProfilesQuery request,
        CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken))
            .Select(profile => new AccessProfileDetailsResponse(
                profile.Code,
                profile.DisplayName,
                profile.Role,
                profile.IsHrAssignable,
                profile.IsActive,
                profile.Permissions))
            .ToArray();
}
