namespace GRD.SpChn.Identity.Domain;

public sealed class AccessProfile
{
    private static readonly IReadOnlySet<string> PrivilegedRoles = new HashSet<string>(
        ["Director", "GeneralManager"],
        StringComparer.OrdinalIgnoreCase);

    public AccessProfile(
        string code,
        string displayName,
        string role,
        bool isHrAssignable,
        bool isActive,
        IReadOnlyCollection<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A profile code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("A role is required.", nameof(role));

        Code = code.Trim();
        DisplayName = displayName.Trim();
        Role = role;
        IsHrAssignable = isHrAssignable;
        IsActive = isActive;
        Permissions = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public string Code { get; }
    public string DisplayName { get; }
    public string Role { get; }
    public bool IsHrAssignable { get; }
    public bool IsActive { get; }
    public IReadOnlyCollection<string> Permissions { get; }

    public bool CanBeAssignedByHr =>
        IsActive &&
        IsHrAssignable &&
        !PrivilegedRoles.Contains(Role);
}
