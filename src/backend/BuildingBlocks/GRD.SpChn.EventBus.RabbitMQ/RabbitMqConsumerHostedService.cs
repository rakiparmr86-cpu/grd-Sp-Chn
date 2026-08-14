using System.Text.Json;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GRD.SpChn.EventBus.RabbitMQ;

internal sealed class RabbitMqConsumerHostedService<TEvent>(
    IOptions<RabbitMqOptions> options,
    RabbitMqSubscription<TEvent> subscription,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumerHostedService<TEvent>> logger) : BackgroundService
    where TEvent : IIntegrationEvent
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "RabbitMQ consumer for {EventType} stopped unexpectedly; reconnecting",
                    typeof(TEvent).Name);

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var rabbitMq = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = rabbitMq.HostName,
            Port = rabbitMq.Port,
            UserName = rabbitMq.UserName,
            Password = rabbitMq.Password,
            VirtualHost = rabbitMq.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        await using var connection = await factory.CreateConnectionAsync(
            $"{rabbitMq.ClientProvidedName}-{subscription.QueueName}",
            stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            subscription.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            subscription.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            subscription.QueueName,
            subscription.ExchangeName,
            subscription.RoutingKey,
            cancellationToken: stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var integrationEvent = JsonSerializer.Deserialize<TEvent>(
                    delivery.Body.Span,
                    SerializerOptions)
                    ?? throw new JsonException(
                        $"The {typeof(TEvent).Name} payload was empty.");

                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<IIntegrationEventHandler<TEvent>>();

                await handler.HandleAsync(integrationEvent, delivery.CancellationToken);
                await channel.BasicAckAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    delivery.CancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Discarding malformed {EventType} message {MessageId}",
                    typeof(TEvent).Name,
                    delivery.BasicProperties.MessageId);
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    delivery.CancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to process {EventType} message {MessageId}; requeueing",
                    typeof(TEvent).Name,
                    delivery.BasicProperties.MessageId);
                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    delivery.CancellationToken);
            }
        };

        await channel.BasicConsumeAsync(
            subscription.QueueName,
            autoAck: false,
            consumer,
            stoppingToken);

        logger.LogInformation(
            "Consuming {EventType} from {Exchange}/{Queue} with routing key {RoutingKey}",
            typeof(TEvent).Name,
            subscription.ExchangeName,
            subscription.QueueName,
            subscription.RoutingKey);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
