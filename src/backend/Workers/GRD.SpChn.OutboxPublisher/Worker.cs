using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using GRD.SpChn.EventBus.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace GRD.SpChn.OutboxPublisher;

public sealed partial class Worker(
    IConfiguration configuration,
    IOptions<OutboxPublisherOptions> options,
    IEventBus eventBus,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var publisherOptions = options.Value;
        var sources = ResolveSources(publisherOptions.Sources);
        if (sources.Count == 0)
        {
            logger.LogWarning(
                "No outbox sources have a configured connection string; " +
                "set ConnectionStrings__OrderDatabase and ConnectionStrings__InventoryDatabase");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = 0;
            foreach (var source in sources)
            {
                try
                {
                    processedCount += await ProcessSourceAsync(
                        source,
                        Math.Max(1, publisherOptions.BatchSize),
                        stoppingToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to publish messages from outbox source {SourceName}",
                        source.Name);
                }
            }

            var delay = processedCount == 0
                ? TimeSpan.FromMilliseconds(
                    Math.Max(100, publisherOptions.PollingIntervalMilliseconds))
                : TimeSpan.FromMilliseconds(50);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private IReadOnlyCollection<RuntimeOutboxSource> ResolveSources(
        IEnumerable<OutboxSourceOptions> configuredSources)
    {
        var sources = new List<RuntimeOutboxSource>();
        foreach (var source in configuredSources)
        {
            if (!SafeIdentifier().IsMatch(source.TableName))
            {
                throw new InvalidOperationException(
                    $"Outbox table name '{source.TableName}' is not a safe SQL identifier.");
            }

            var connectionString = configuration.GetConnectionString(
                source.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                logger.LogWarning(
                    "Skipping outbox source {SourceName}: connection string {ConnectionStringName} is missing",
                    source.Name,
                    source.ConnectionStringName);
                continue;
            }

            sources.Add(new RuntimeOutboxSource(
                source.Name,
                source.TableName,
                connectionString));
        }

        return sources;
    }

    private async Task<int> ProcessSourceAsync(
        RuntimeOutboxSource source,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(source.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var processedCount = 0;

        while (processedCount < batchSize)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            OutboxRow? message = null;

            try
            {
                message = await connection.QuerySingleOrDefaultAsync<OutboxRow>(
                    new CommandDefinition(
                        $"""
                        SELECT id AS Id,
                               event_id AS EventId,
                               event_type AS EventType,
                               exchange_name AS ExchangeName,
                               routing_key AS RoutingKey,
                               payload AS Payload,
                               occurred_on_utc AS OccurredOnUtc
                        FROM `{source.TableName}`
                        WHERE processed_on_utc IS NULL
                          AND available_on_utc <= UTC_TIMESTAMP(6)
                        ORDER BY occurred_on_utc, id
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED;
                        """,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                if (message is null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    break;
                }

                await eventBus.PublishRawAsync(
                    message.ExchangeName,
                    message.RoutingKey,
                    message.EventType,
                    message.EventId,
                    DateTime.SpecifyKind(message.OccurredOnUtc, DateTimeKind.Utc),
                    Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken);

                await connection.ExecuteAsync(new CommandDefinition(
                    $"""
                    UPDATE `{source.TableName}`
                    SET processed_on_utc = @ProcessedOnUtc,
                        last_error = NULL
                    WHERE id = @Id;
                    """,
                    new
                    {
                        message.Id,
                        ProcessedOnUtc = DateTime.UtcNow
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                await transaction.CommitAsync(cancellationToken);
                processedCount++;
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (message is not null)
                {
                    await RecordFailureAsync(source, connection, message.Id, exception, cancellationToken);
                }

                throw;
            }
        }

        return processedCount;
    }

    private static Task RecordFailureAsync(
        RuntimeOutboxSource source,
        MySqlConnection connection,
        Guid messageId,
        Exception exception,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE `{source.TableName}`
            SET retry_count = retry_count + 1,
                last_error = @LastError,
                available_on_utc = DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 5 SECOND)
            WHERE id = @Id
              AND processed_on_utc IS NULL;
            """,
            new
            {
                Id = messageId,
                LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)]
            },
            cancellationToken: cancellationToken));

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex SafeIdentifier();

    private sealed record RuntimeOutboxSource(
        string Name,
        string TableName,
        string ConnectionString);

    private sealed record OutboxRow(
        Guid Id,
        Guid EventId,
        string EventType,
        string ExchangeName,
        string RoutingKey,
        string Payload,
        DateTime OccurredOnUtc);
}
