using System.Net;
using System.Net.Mail;
using Dapper;
using GRD.SpChn.Persistence.MySql;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GRD.SpChn.Notifications.Infrastructure.Email;

internal sealed class EmailDeliveryWorker(
    IDbConnectionFactory connectionFactory,
    IOptions<SmtpOptions> options,
    ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var smtp = options.Value;
        if (!smtp.Enabled)
        {
            logger.LogWarning(
                "SMTP delivery is disabled. Activity emails will remain queued in notification_email_deliveries.");
            return;
        }

        Validate(smtp);
        while (!stoppingToken.IsCancellationRequested)
        {
            var delivered = await DeliverNextAsync(smtp, stoppingToken);
            if (!delivered)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, smtp.PollingIntervalSeconds)),
                    stoppingToken);
            }
        }
    }

    private async Task<bool> DeliverNextAsync(
        SmtpOptions smtp,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var delivery = await connection.QuerySingleOrDefaultAsync<EmailDeliveryRow>(new CommandDefinition(
            """
            SELECT id AS Id, recipient_email AS RecipientEmail,
                   subject AS Subject, body AS Body, attempt_count AS AttemptCount
            FROM notification_email_deliveries
            WHERE status IN ('Pending', 'Failed')
              AND attempt_count < @MaxAttempts
              AND available_on_utc <= UTC_TIMESTAMP(6)
            ORDER BY created_on_utc, id
            LIMIT 1;
            """,
            new { smtp.MaxAttempts },
            cancellationToken: cancellationToken));
        if (delivery is null) return false;

        var claimed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE notification_email_deliveries
            SET status = 'Sending', updated_on_utc = UTC_TIMESTAMP(6)
            WHERE id = @Id AND status IN ('Pending', 'Failed');
            """,
            new { delivery.Id },
            cancellationToken: cancellationToken));
        if (claimed != 1) return true;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, smtp.FromName),
                Subject = delivery.Subject,
                Body = delivery.Body,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(delivery.RecipientEmail));

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(smtp.UserName)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(smtp.UserName, smtp.Password)
            };
            await client.SendMailAsync(message, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE notification_email_deliveries
                SET status = 'Sent', sent_on_utc = UTC_TIMESTAMP(6),
                    attempt_count = attempt_count + 1, last_error = NULL,
                    updated_on_utc = UTC_TIMESTAMP(6)
                WHERE id = @Id;
                """,
                new { delivery.Id },
                cancellationToken: cancellationToken));
            logger.LogInformation("Sent activity email {DeliveryId} to {RecipientEmail}",
                delivery.Id, delivery.RecipientEmail);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var delayMinutes = Math.Min(30, (int)Math.Pow(2, delivery.AttemptCount));
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE notification_email_deliveries
                SET status = 'Failed', attempt_count = attempt_count + 1,
                    last_error = @LastError,
                    available_on_utc = DATE_ADD(UTC_TIMESTAMP(6), INTERVAL @DelayMinutes MINUTE),
                    updated_on_utc = UTC_TIMESTAMP(6)
                WHERE id = @Id;
                """,
                new
                {
                    delivery.Id,
                    LastError = exception.Message[..Math.Min(2000, exception.Message.Length)],
                    DelayMinutes = delayMinutes
                },
                cancellationToken: cancellationToken));
            logger.LogError(exception, "Activity email {DeliveryId} could not be delivered", delivery.Id);
        }

        return true;
    }

    private static void Validate(SmtpOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new InvalidOperationException("Smtp:Host is required when SMTP delivery is enabled.");
        if (string.IsNullOrWhiteSpace(options.FromAddress))
            throw new InvalidOperationException("Smtp:FromAddress is required when SMTP delivery is enabled.");
    }

    private sealed record EmailDeliveryRow(
        Guid Id,
        string RecipientEmail,
        string Subject,
        string Body,
        int AttemptCount);
}
