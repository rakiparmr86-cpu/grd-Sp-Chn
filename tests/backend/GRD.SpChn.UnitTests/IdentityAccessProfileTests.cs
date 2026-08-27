using GRD.SpChn.Identity.Domain;
using GRD.SpChn.Security;

namespace GRD.SpChn.UnitTests;

public sealed class IdentityAccessProfileTests
{
    [Fact]
    public void Hr_manager_can_create_users_without_organization_admin_access()
    {
        var profile = new AccessProfile(
            "HrManager",
            "HR Manager",
            ErpRoles.Manager,
            isHrAssignable: true,
            isActive: true,
            [ErpPermissions.OrganizationRead, ErpPermissions.IdentityUserCreate]);

        Assert.True(profile.CanBeAssignedByHr);
        Assert.Equal(ErpRoles.Manager, profile.Role);
        Assert.Contains(ErpPermissions.IdentityUserCreate, profile.Permissions);
        Assert.Contains(ErpPermissions.OrganizationRead, profile.Permissions);
        Assert.DoesNotContain(ErpPermissions.OrganizationManage, profile.Permissions);
    }

    [Fact]
    public void Privileged_roles_cannot_be_made_hr_assignable_by_database_configuration()
    {
        var director = new AccessProfile(
            "Director",
            "Director",
            ErpRoles.Director,
            isHrAssignable: true,
            isActive: true,
            [ErpPermissions.OrganizationManage]);
        var generalManager = new AccessProfile(
            "RegionalGeneralManager",
            "Regional General Manager",
            ErpRoles.GeneralManager,
            isHrAssignable: true,
            isActive: true,
            [ErpPermissions.OrganizationRead]);

        Assert.False(director.CanBeAssignedByHr);
        Assert.False(generalManager.CanBeAssignedByHr);
    }

    [Fact]
    public void Access_profile_removes_duplicate_permissions()
    {
        var profile = new AccessProfile(
            "StoreExecutive",
            "Store Executive",
            ErpRoles.Executive,
            isHrAssignable: true,
            isActive: true,
            [
                ErpPermissions.OrganizationRead,
                ErpPermissions.OrganizationRead,
                ErpPermissions.InventoryStockRead
            ]);

        Assert.Equal(2, profile.Permissions.Count);
    }
}
