namespace GRD.SpChn.Organization.Domain;

public sealed class OrganizationUnit
{
    private OrganizationUnit(
        Guid id,
        Guid? parentId,
        string code,
        string name,
        OrganizationUnitType type,
        bool isActive,
        DateTime createdOnUtc)
    {
        Id = id;
        ParentId = parentId;
        Code = code;
        Name = name;
        Type = type;
        IsActive = isActive;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid Id { get; }
    public Guid? ParentId { get; }
    public string Code { get; }
    public string Name { get; }
    public OrganizationUnitType Type { get; }
    public bool IsActive { get; }
    public DateTime CreatedOnUtc { get; }

    public static OrganizationUnit Create(
        Guid? parentId,
        string code,
        string name,
        OrganizationUnitType type,
        OrganizationUnitType? parentType,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A unit code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A unit name is required.", nameof(name));
        ValidateParent(type, parentId, parentType);

        return new OrganizationUnit(
            Guid.NewGuid(),
            parentId,
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            type,
            true,
            utcNow ?? DateTime.UtcNow);
    }

    public static OrganizationUnit Rehydrate(
        Guid id,
        Guid? parentId,
        string code,
        string name,
        OrganizationUnitType type,
        bool isActive,
        DateTime createdOnUtc) =>
        new(id, parentId, code, name, type, isActive, createdOnUtc);

    private static void ValidateParent(
        OrganizationUnitType type,
        Guid? parentId,
        OrganizationUnitType? parentType)
    {
        if (type == OrganizationUnitType.Enterprise)
        {
            if (parentId is not null) throw new ArgumentException("Enterprise cannot have a parent.", nameof(parentId));
            return;
        }

        if (parentId is null || parentType is null)
        {
            throw new ArgumentException($"{type} requires a parent organization unit.", nameof(parentId));
        }

        var allowed = type switch
        {
            OrganizationUnitType.HeadOffice => parentType == OrganizationUnitType.Enterprise,
            OrganizationUnitType.RegionalOffice => parentType == OrganizationUnitType.HeadOffice,
            OrganizationUnitType.HeadBranch => parentType == OrganizationUnitType.RegionalOffice,
            OrganizationUnitType.Branch => parentType is OrganizationUnitType.HeadBranch or OrganizationUnitType.RegionalOffice,
            OrganizationUnitType.ManufacturingPlant or
            OrganizationUnitType.Warehouse or
            OrganizationUnitType.SalesBranch or
            OrganizationUnitType.ConsumptionUnit => parentType is OrganizationUnitType.Branch or OrganizationUnitType.HeadBranch,
            _ => false
        };

        if (!allowed)
        {
            throw new ArgumentException(
                $"A {type} cannot be created below {parentType}.",
                nameof(parentId));
        }
    }
}
