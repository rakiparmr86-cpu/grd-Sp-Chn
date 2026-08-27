using GRD.SpChn.Organization.Domain;

namespace GRD.SpChn.Organization.Application.OrganizationUnits;

public sealed record OrganizationUnitResponse(
    Guid Id,
    Guid? ParentId,
    string Code,
    string Name,
    string Type,
    bool IsActive)
{
    public static OrganizationUnitResponse From(OrganizationUnit unit) =>
        new(unit.Id, unit.ParentId, unit.Code, unit.Name, unit.Type.ToString(), unit.IsActive);
}
