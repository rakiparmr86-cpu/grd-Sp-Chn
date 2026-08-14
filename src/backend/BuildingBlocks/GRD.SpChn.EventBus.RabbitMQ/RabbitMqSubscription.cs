using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.EventBus.RabbitMQ;

internal sealed record RabbitMqSubscription<TEvent>(
    string ExchangeName,
    string QueueName,
    string RoutingKey)
    where TEvent : IIntegrationEvent;
