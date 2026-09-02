using Dapper;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Notifications.Application.Abstractions;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Notifications.Infrastructure.Email;

internal sealed class EmailNotificationQueue(IDbConnectionFactory connectionFactory)
    : IEmailNotificationQueue
{
    public async Task QueueAsync(
        ActivityNotificationRequestedIntegrationEvent notification,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var recipients = new Dictionary<Guid, RecipientRow>();

        var directUserIds = notification.RecipientUserIds.Distinct().ToArray();
        if (directUserIds.Length > 0)
        {
            var directRecipients = await connection.QueryAsync<RecipientRow>(new CommandDefinition(
                """
                SELECT id AS UserId, email AS Email
                FROM identity_users
                WHERE id IN @UserIds
                  AND is_active = TRUE
                  AND email LIKE '%@yopmail.com';
                """,
                new { UserIds = directUserIds },
                cancellationToken: cancellationToken));
            foreach (var recipient in directRecipients) recipients[recipient.UserId] = recipient;
        }

        var permissionCodes = notification.RecipientPermissionCodes.Distinct().ToArray();
        if (permissionCodes.Length > 0)
        {
            var permissionRecipients = await connection.QueryAsync<RecipientRow>(new CommandDefinition(
                """
                SELECT DISTINCT user_account.id AS UserId, user_account.email AS Email
                FROM identity_users user_account
                INNER JOIN identity_access_profile_permissions profile_permission
                        ON profile_permission.access_profile_code = user_account.access_profile_code
                WHERE profile_permission.permission_code IN @PermissionCodes
                  AND user_account.is_active = TRUE
                  AND user_account.email LIKE '%@yopmail.com';
                """,
                new { PermissionCodes = permissionCodes },
                cancellationToken: cancellationToken));
            foreach (var recipient in permissionRecipients) recipients[recipient.UserId] = recipient;
        }

        if (recipients.Count == 0) return;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var recipient in recipients.Values)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT IGNORE INTO notification_email_deliveries
                    (id, event_id, activity_code, reference_type, reference_id,
                     recipient_user_id, recipient_email, subject, body, status,
                     attempt_count, available_on_utc, created_on_utc, updated_on_utc)
                VALUES
                    (@Id, @EventId, @ActivityCode, @ReferenceType, @ReferenceId,
                     @RecipientUserId, @RecipientEmail, @Subject, @Body, 'Pending',
                     0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    notification.EventId,
                    notification.ActivityCode,
                    notification.ReferenceType,
                    notification.ReferenceId,
                    RecipientUserId = recipient.UserId,
                    RecipientEmail = recipient.Email,
                    notification.Subject,
                    notification.Body
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record RecipientRow(Guid UserId, string Email);
}
