using System.Text.Json;
using System.Text;
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
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        await channel.ExchangeDeclareAsync(
            subscription.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);
        var deadLetterExchangeName = $"{subscription.ExchangeName}.dead-letter";
        var deadLetterQueueName = $"{subscription.QueueName}.dead-letter";
        await channel.ExchangeDeclareAsync(
            deadLetterExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            deadLetterQueueName,
            deadLetterExchangeName,
            subscription.RoutingKey,
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
            TEvent integrationEvent;
            try
            {
                integrationEvent = JsonSerializer.Deserialize<TEvent>(
                    delivery.Body.Span,
                    SerializerOptions)
                    ?? throw new JsonException(
                        $"The {typeof(TEvent).Name} payload was empty.");

            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Dead-lettering malformed {EventType} message {MessageId}",
                    typeof(TEvent).Name,
                    delivery.BasicProperties.MessageId);
                await DeadLetterOrRequeueAsync(
                    channel,
                    delivery,
                    deadLetterExchangeName,
                    exception,
                    delivery.CancellationToken);
                return;
            }

            var maxAttempts = Math.Max(1, rabbitMq.ConsumerMaxRetryAttempts);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider
                        .GetRequiredService<IIntegrationEventHandler<TEvent>>();

                    await handler.HandleAsync(integrationEvent, delivery.CancellationToken);
                    await channel.BasicAckAsync(
                        delivery.DeliveryTag,
                        multiple: false,
                        delivery.CancellationToken);
                    return;
                }
                catch (Exception exception) when (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(
                        Math.Max(0, rabbitMq.ConsumerRetryDelayMilliseconds) *
                        Math.Pow(2, attempt - 1));
                    logger.LogWarning(
                        exception,
                        "Attempt {Attempt}/{MaxAttempts} failed for {EventType} message " +
                        "{MessageId}; retrying in {RetryDelay}",
                        attempt,
                        maxAttempts,
                        typeof(TEvent).Name,
                        delivery.BasicProperties.MessageId,
                        delay);
                    await Task.Delay(delay, delivery.CancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "All {MaxAttempts} attempts failed for {EventType} message " +
                        "{MessageId}; moving it to {DeadLetterExchange}",
                        maxAttempts,
                        typeof(TEvent).Name,
                        delivery.BasicProperties.MessageId,
                        deadLetterExchangeName);
                    await DeadLetterOrRequeueAsync(
                        channel,
                        delivery,
                        deadLetterExchangeName,
                        exception,
                        delivery.CancellationToken);
                    return;
                }
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

    private async Task DeadLetterOrRequeueAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        string deadLetterExchangeName,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var headers = delivery.BasicProperties.Headers is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(delivery.BasicProperties.Headers);
            headers["x-grd-error-type"] =
                Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name);
            headers["x-grd-failed-on-utc"] =
                Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"));

            var properties = new BasicProperties
            {
                ContentType = delivery.BasicProperties.ContentType ?? "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = delivery.BasicProperties.MessageId,
                Type = delivery.BasicProperties.Type ?? typeof(TEvent).Name,
                Timestamp = delivery.BasicProperties.Timestamp,
                Headers = headers
            };

            await channel.BasicPublishAsync(
                deadLetterExchangeName,
                subscription.RoutingKey,
                mandatory: true,
                properties,
                delivery.Body,
                cancellationToken);
            await channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false,
                cancellationToken);
        }
        catch (Exception deadLetterException)
        {
            logger.LogError(
                deadLetterException,
                "Could not dead-letter {EventType} message {MessageId}; requeueing it",
                typeof(TEvent).Name,
                delivery.BasicProperties.MessageId);
            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken);
        }
    }
}
