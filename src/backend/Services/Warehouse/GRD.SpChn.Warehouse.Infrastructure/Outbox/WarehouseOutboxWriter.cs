using System.Text.Json;
using Dapper;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Warehouse.Application.Abstractions;
using GRD.SpChn.Warehouse.Infrastructure.Persistence;

namespace GRD.SpChn.Warehouse.Infrastructure.Outbox;

internal sealed class WarehouseOutboxWriter(WarehouseUnitOfWork unitOfWork)
    : IWarehouseOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task AddAsync(
        IIntegrationEvent integrationEvent,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO warehouse_outbox
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
