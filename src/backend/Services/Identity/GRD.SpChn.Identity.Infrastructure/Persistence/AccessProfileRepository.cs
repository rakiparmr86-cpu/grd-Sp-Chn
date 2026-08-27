using Dapper;
using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.Identity.Domain;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Identity.Infrastructure.Persistence;

internal sealed class AccessProfileRepository(IDbConnectionFactory connectionFactory)
    : IAccessProfileRepository
{
    public async Task<AccessProfile?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT code AS Code,
                   display_name AS DisplayName,
                   role_name AS Role,
                   is_hr_assignable AS IsHrAssignable,
                   is_active AS IsActive
            FROM identity_access_profiles
            WHERE code = @Code
            LIMIT 1;

            SELECT permission_code
            FROM identity_access_profile_permissions
            WHERE access_profile_code = @Code
            ORDER BY permission_code;
            """,
            new { Code = code },
            cancellationToken: cancellationToken));

        var row = await result.ReadSingleOrDefaultAsync<AccessProfileRow>();
        var permissions = (await result.ReadAsync<string>()).ToArray();
        return row is null ? null : Map(row, permissions);
    }

    public async Task<IReadOnlyCollection<AccessProfile>> GetHrAssignableAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<AccessProfilePermissionRow>(new CommandDefinition(
            """
            SELECT profile.code AS Code,
                   profile.display_name AS DisplayName,
                   profile.role_name AS Role,
                   profile.is_hr_assignable AS IsHrAssignable,
                   profile.is_active AS IsActive,
                   permission.permission_code AS Permission
            FROM identity_access_profiles profile
            LEFT JOIN identity_access_profile_permissions permission
                   ON permission.access_profile_code = profile.code
            WHERE profile.is_hr_assignable = TRUE
              AND profile.is_active = TRUE
            ORDER BY profile.display_name, permission.permission_code;
            """,
            cancellationToken: cancellationToken))).ToArray();

        return rows
            .GroupBy(row => new
            {
                row.Code,
                row.DisplayName,
                row.Role,
                row.IsHrAssignable,
                row.IsActive
            })
            .Select(group => new AccessProfile(
                group.Key.Code,
                group.Key.DisplayName,
                group.Key.Role,
                group.Key.IsHrAssignable,
                group.Key.IsActive,
                group.Select(row => row.Permission)
                    .OfType<string>()
                    .Where(permission => !string.IsNullOrWhiteSpace(permission))
                    .ToArray()))
            .ToArray();
    }

    private static AccessProfile Map(
        AccessProfileRow row,
        IReadOnlyCollection<string> permissions) =>
        new(
            row.Code,
            row.DisplayName,
            row.Role,
            row.IsHrAssignable,
            row.IsActive,
            permissions);

    private sealed record AccessProfileRow(
        string Code,
        string DisplayName,
        string Role,
        bool IsHrAssignable,
        bool IsActive);

    private sealed record AccessProfilePermissionRow(
        string Code,
        string DisplayName,
        string Role,
        bool IsHrAssignable,
        bool IsActive,
        string? Permission);
}
