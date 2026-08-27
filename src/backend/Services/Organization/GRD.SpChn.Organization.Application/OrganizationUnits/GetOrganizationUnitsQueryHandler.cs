using GRD.SpChn.Organization.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Organization.Application.OrganizationUnits;

internal sealed class GetOrganizationUnitsQueryHandler(IOrganizationUnitRepository repository)
    : IRequestHandler<GetOrganizationUnitsQuery, IReadOnlyCollection<OrganizationUnitResponse>>
{
    public async Task<IReadOnlyCollection<OrganizationUnitResponse>> Handle(
        GetOrganizationUnitsQuery request,
        CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken))
            .Select(OrganizationUnitResponse.From)
            .ToArray();
}
