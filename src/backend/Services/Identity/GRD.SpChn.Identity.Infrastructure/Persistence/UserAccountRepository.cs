using Dapper;
using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.Identity.Domain;
using GRD.SpChn.Persistence.MySql;
using MySqlConnector;

namespace GRD.SpChn.Identity.Infrastructure.Persistence;

internal sealed class UserAccountRepository(IDbConnectionFactory connectionFactory)
    : IUserAccountRepository
{
    public async Task<UserAccount?> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT user_account.id AS Id,
                   user_account.user_name AS UserName,
                   user_account.password_hash AS PasswordHash,
                   access_profile.role_name AS Role,
                   access_profile.code AS AccessProfileCode,
                   user_account.organization_unit_id AS OrganizationUnitId,
                   (user_account.is_active AND access_profile.is_active) AS IsActive
            FROM identity_users user_account
            INNER JOIN identity_access_profiles access_profile
                    ON access_profile.code = user_account.access_profile_code
            WHERE user_account.normalized_user_name = UPPER(@UserName)
            LIMIT 1;

            SELECT profile_permission.permission_code
            FROM identity_users user_account
            INNER JOIN identity_access_profiles access_profile
                    ON access_profile.code = user_account.access_profile_code
            INNER JOIN identity_access_profile_permissions profile_permission
                    ON profile_permission.access_profile_code = access_profile.code
            WHERE user_account.normalized_user_name = UPPER(@UserName)
              AND access_profile.is_active = TRUE
            ORDER BY profile_permission.permission_code;
            """,
            new { UserName = userName },
            cancellationToken: cancellationToken));

        var row = await result.ReadSingleOrDefaultAsync<UserRow>();
        var permissions = (await result.ReadAsync<string>()).ToArray();
        return row is null
            ? null
            : new UserAccount(
                row.Id,
                row.UserName,
                row.PasswordHash,
                row.Role,
                row.AccessProfileCode,
                row.OrganizationUnitId,
                row.IsActive,
                permissions);
    }

    public async Task<bool> TryAddAsync(
        UserAccount user,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO identity_users
                    (id, user_name, normalized_user_name, password_hash, role_name,
                     access_profile_code, organization_unit_id, is_active, created_on_utc)
                VALUES
                    (@Id, @UserName, UPPER(@UserName), @PasswordHash, @Role,
                     @AccessProfileCode, @OrganizationUnitId, @IsActive, UTC_TIMESTAMP(6));
                """,
                new
                {
                    user.Id,
                    user.UserName,
                    user.PasswordHash,
                    user.Role,
                    user.AccessProfileCode,
                    user.OrganizationUnitId,
                    user.IsActive
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed record UserRow(
        Guid Id,
        string UserName,
        string PasswordHash,
        string Role,
        string AccessProfileCode,
        Guid OrganizationUnitId,
        bool IsActive);
}
