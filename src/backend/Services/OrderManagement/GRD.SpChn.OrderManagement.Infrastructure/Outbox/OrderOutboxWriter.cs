using System.Text.Json;
using Dapper;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.OrderManagement.Application.Abstractions;
using GRD.SpChn.OrderManagement.Infrastructure.Persistence;

namespace GRD.SpChn.OrderManagement.Infrastructure.Outbox;

internal sealed class OrderOutboxWriter(OrderUnitOfWork unitOfWork) : IOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task AddAsync(
        IIntegrationEvent integrationEvent,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO order_management_outbox
                (id, event_id, event_type, exchange_name, routing_key, payload,
                 occurred_on_utc, available_on_utc)
            VALUES
                (@Id, @EventId, @EventType, @ExchangeName, @RoutingKey, @Payload,
                 @OccurredOnUtc, @AvailableOnUtc);
            """,
            new
            {
                Id = Guid.NewGuid(),
                integrationEvent.EventId,
                EventType = integrationEvent.GetType().Name,
                ExchangeName = exchangeName,
                RoutingKey = routingKey,
                Payload = JsonSerializer.Serialize(
                    integrationEvent,
                    integrationEvent.GetType(),
                    SerializerOptions),
                integrationEvent.OccurredOnUtc,
                AvailableOnUtc = integrationEvent.OccurredOnUtc
            },
            unitOfWork.Transaction,
            cancellationToken: cancellationToken));
}
