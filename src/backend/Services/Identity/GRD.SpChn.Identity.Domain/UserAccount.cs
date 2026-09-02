namespace GRD.SpChn.Identity.Domain;

public sealed class UserAccount
{
    public UserAccount(
        Guid id,
        string userName,
        string email,
        string passwordHash,
        string role,
        string accessProfileCode,
        Guid organizationUnitId,
        bool isActive,
        IReadOnlyCollection<string> permissions)
    {
        if (id == Guid.Empty) throw new ArgumentException("A user id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("A user name is required.", nameof(userName));
        if (string.IsNullOrWhiteSpace(email) || !email.EndsWith("@yopmail.com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A Yopmail notification email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("A role is required.", nameof(role));
        if (string.IsNullOrWhiteSpace(accessProfileCode)) throw new ArgumentException("An access profile is required.", nameof(accessProfileCode));
        if (organizationUnitId == Guid.Empty) throw new ArgumentException("An organization unit is required.", nameof(organizationUnitId));

        Id = id;
        UserName = userName.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
        AccessProfileCode = accessProfileCode.Trim();
        OrganizationUnitId = organizationUnitId;
        IsActive = isActive;
        Permissions = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public Guid Id { get; }
    public string UserName { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public string Role { get; }
    public string AccessProfileCode { get; }
    public Guid OrganizationUnitId { get; }
    public bool IsActive { get; }
    public IReadOnlyCollection<string> Permissions { get; }
}
