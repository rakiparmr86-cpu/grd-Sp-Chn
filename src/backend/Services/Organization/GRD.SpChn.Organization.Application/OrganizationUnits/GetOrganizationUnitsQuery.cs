using MediatR;

namespace GRD.SpChn.Organization.Application.OrganizationUnits;

public sealed record GetOrganizationUnitsQuery
    : IRequest<IReadOnlyCollection<OrganizationUnitResponse>>;
