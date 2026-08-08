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

    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

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
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        var eventType = typeof(TEvent).Name;
        var body = JsonSerializer.SerializeToUtf8Bytes(
            integrationEvent,
            integrationEvent.GetType());
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = integrationEvent.EventId.ToString(),
            Type = eventType,
            Timestamp = new AmqpTimestamp(
                new DateTimeOffset(integrationEvent.OccurredOnUtc).ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            _options.ExchangeName,
            eventType,
            mandatory: true,
            properties,
            body,
            cancellationToken);

        logger.LogInformation(
            "Published integration event {EventType} with id {EventId}",
            eventType,
            integrationEvent.EventId);
    }
}
