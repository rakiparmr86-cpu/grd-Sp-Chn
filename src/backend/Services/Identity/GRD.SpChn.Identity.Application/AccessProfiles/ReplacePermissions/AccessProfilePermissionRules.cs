using GRD.SpChn.Security;

namespace GRD.SpChn.Identity.Application.AccessProfiles.ReplacePermissions;

public static class AccessProfilePermissionRules
{
    public static bool PreservesRequiredAdministration(
        string accessProfileCode,
        IReadOnlyCollection<string> permissionCodes) =>
        !accessProfileCode.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
        permissionCodes.Contains(
            ErpPermissions.IdentityAccessProfileManage,
            StringComparer.Ordinal);
}
