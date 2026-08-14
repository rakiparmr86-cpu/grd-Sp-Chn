using System.Text.Json;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace GRD.SpChn.EventBus.RabbitMQ;

internal sealed class RabbitMqEventBus(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventBus> logger) : IEventBus
{
    private readonly RabbitMqOptions _options = options.Value;
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType().Name;
        var body = JsonSerializer.SerializeToUtf8Bytes(
            integrationEvent,
            integrationEvent.GetType(),
            SerializerOptions);

        await PublishRawAsync(
            _options.ExchangeName,
            eventType,
            eventType,
            integrationEvent.EventId,
            integrationEvent.OccurredOnUtc,
            body,
            cancellationToken);
    }

    public async Task PublishRawAsync(
        string exchangeName,
        string routingKey,
        string eventType,
        Guid eventId,
        DateTime occurredOnUtc,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var connectionFactory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        await using var connection = await connectionFactory.CreateConnectionAsync(
            _options.ClientProvidedName,
            cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = eventId.ToString(),
            Type = eventType,
            Timestamp = new AmqpTimestamp(
                new DateTimeOffset(occurredOnUtc).ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchangeName,
            routingKey,
            mandatory: true,
            properties,
            payload,
            cancellationToken);

        logger.LogInformation(
            "Published integration event {EventType} with id {EventId}",
            eventType,
            eventId);
    }
}
