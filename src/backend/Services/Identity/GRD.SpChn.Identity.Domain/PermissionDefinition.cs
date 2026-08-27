namespace GRD.SpChn.Identity.Domain;

public sealed record PermissionDefinition(
    string Code,
    string DisplayName,
    string Module,
    string Description,
    bool IsActive);
