using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Organization.Application.OrganizationUnits;

public sealed record CreateOrganizationUnitCommand(
    Guid? ParentId,
    string Code,
    string Name,
    string Type) : IRequest<Result<OrganizationUnitResponse>>;
