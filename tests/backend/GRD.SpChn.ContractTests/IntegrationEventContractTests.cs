using System.Text.Json;
using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.ContractTests;

public sealed class IntegrationEventContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Concrete_integration_events_follow_the_public_contract_convention()
    {
        var contractTypes = typeof(IIntegrationEvent).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(contractTypes);

        foreach (var contractType in contractTypes)
        {
            Assert.True(
                contractType.IsSealed,
                $"Integration event '{contractType.Name}' must be sealed.");
            Assert.EndsWith("IntegrationEvent", contractType.Name);
            Assert.True(
                typeof(IntegrationEvent).IsAssignableFrom(contractType),
                $"Integration event '{contractType.Name}' must use the shared envelope.");
        }
    }

    [Fact]
    public void Integration_event_envelope_assigns_unique_identity_and_utc_time()
    {
        var first = new OrderPlacedIntegrationEvent(
            Guid.NewGuid(),
            "ORD-001",
            Guid.NewGuid(),
            [new OrderPlacedItem(Guid.NewGuid(), 1)]);
        var second = new OrderPlacedIntegrationEvent(
            Guid.NewGuid(),
            "ORD-002",
            Guid.NewGuid(),
            [new OrderPlacedItem(Guid.NewGuid(), 2)]);

        Assert.NotEqual(Guid.Empty, first.EventId);
        Assert.NotEqual(first.EventId, second.EventId);
        Assert.Equal(IntegrationEvent.InitialSchemaVersion, first.SchemaVersion);
        Assert.Equal(IntegrationEvent.InitialSchemaVersion, second.SchemaVersion);
        Assert.Equal(DateTimeKind.Utc, first.OccurredOnUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, second.OccurredOnUtc.Kind);
    }

    [Fact]
    public void Phase_6_contracts_round_trip_without_losing_required_data()
    {
        var occurredOnUtc = new DateTime(
            2026,
            8,
            25,
            10,
            30,
            0,
            DateTimeKind.Utc);
        var orderPlaced = new OrderPlacedIntegrationEvent(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "ORD-20260825-001",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [
                new OrderPlacedItem(
                    Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    2)
            ])
        {
            EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OccurredOnUtc = occurredOnUtc
        };
        var stockReserved = new StockReservedIntegrationEvent(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            orderPlaced.OrderId,
            [new StockReservedItem(orderPlaced.Items.Single().ProductId, 2)])
        {
            EventId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OccurredOnUtc = occurredOnUtc.AddSeconds(1)
        };
        var stockReservationFailed = new StockReservationFailedIntegrationEvent(
            orderPlaced.OrderId,
            "Insufficient stock.")
        {
            EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            OccurredOnUtc = occurredOnUtc.AddSeconds(2)
        };

        var orderPlacedCopy = RoundTrip(orderPlaced);
        AssertEnvelopeEqual(orderPlaced, orderPlacedCopy);
        Assert.Equal(orderPlaced.OrderId, orderPlacedCopy.OrderId);
        Assert.Equal(orderPlaced.OrderNumber, orderPlacedCopy.OrderNumber);
        Assert.Equal(orderPlaced.CustomerId, orderPlacedCopy.CustomerId);
        Assert.Equal(orderPlaced.Items.ToArray(), orderPlacedCopy.Items.ToArray());

        var stockReservedCopy = RoundTrip(stockReserved);
        AssertEnvelopeEqual(stockReserved, stockReservedCopy);
        Assert.Equal(stockReserved.ReservationId, stockReservedCopy.ReservationId);
        Assert.Equal(stockReserved.OrderId, stockReservedCopy.OrderId);
        Assert.Equal(stockReserved.Items.ToArray(), stockReservedCopy.Items.ToArray());

        var stockReservationFailedCopy = RoundTrip(stockReservationFailed);
        AssertEnvelopeEqual(stockReservationFailed, stockReservationFailedCopy);
        Assert.Equal(
            stockReservationFailed.OrderId,
            stockReservationFailedCopy.OrderId);
        Assert.Equal(
            stockReservationFailed.Reason,
            stockReservationFailedCopy.Reason);
    }

    [Fact]
    public void V1_required_wire_fields_remain_present()
    {
        var orderPlaced = new OrderPlacedIntegrationEvent(
            Guid.NewGuid(),
            "ORD-001",
            Guid.NewGuid(),
            [new OrderPlacedItem(Guid.NewGuid(), 1)]);
        var stockReserved = new StockReservedIntegrationEvent(
            Guid.NewGuid(),
            orderPlaced.OrderId,
            [new StockReservedItem(orderPlaced.Items.Single().ProductId, 1)]);
        var stockReservationFailed = new StockReservationFailedIntegrationEvent(
            orderPlaced.OrderId,
            "No stock record.");

        AssertRequiredProperties(
            orderPlaced,
            "schemaVersion",
            "eventId",
            "occurredOnUtc",
            "orderId",
            "orderNumber",
            "customerId",
            "items");
        AssertRequiredProperties(
            stockReserved,
            "schemaVersion",
            "eventId",
            "occurredOnUtc",
            "reservationId",
            "orderId",
            "items");
        AssertRequiredProperties(
            stockReservationFailed,
            "schemaVersion",
            "eventId",
            "occurredOnUtc",
            "orderId",
            "reason");
    }

    [Fact]
    public void Historical_v1_payload_without_schema_version_remains_readable()
    {
        const string historicalPayload = """
            {
              "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "orderNumber": "ORD-001",
              "customerId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "items": [
                {
                  "productId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
                  "quantity": 2
                }
              ],
              "eventId": "11111111-1111-1111-1111-111111111111",
              "occurredOnUtc": "2026-08-25T10:30:00Z"
            }
            """;

        var integrationEvent = JsonSerializer.Deserialize<OrderPlacedIntegrationEvent>(
            historicalPayload,
            SerializerOptions);

        Assert.NotNull(integrationEvent);
        Assert.Equal(IntegrationEvent.InitialSchemaVersion, integrationEvent.SchemaVersion);
        Assert.Equal("ORD-001", integrationEvent.OrderNumber);
        Assert.Equal(2, integrationEvent.Items.Single().Quantity);
        Assert.Equal(DateTimeKind.Utc, integrationEvent.OccurredOnUtc.Kind);
    }

    [Fact]
    public void V1_reader_ignores_unknown_additive_fields()
    {
        const string payloadFromNewerCompatibleProducer = """
            {
              "orderId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "orderNumber": "ORD-001",
              "customerId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "items": [],
              "eventId": "11111111-1111-1111-1111-111111111111",
              "occurredOnUtc": "2026-08-25T10:30:00Z",
              "schemaVersion": 1,
              "futureOptionalField": "safe additive value"
            }
            """;

        var integrationEvent = JsonSerializer.Deserialize<OrderPlacedIntegrationEvent>(
            payloadFromNewerCompatibleProducer,
            SerializerOptions);

        Assert.NotNull(integrationEvent);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), integrationEvent.OrderId);
        Assert.Equal(IntegrationEvent.InitialSchemaVersion, integrationEvent.SchemaVersion);
    }

    [Fact]
    public void Contracts_assembly_has_no_service_layer_dependency()
    {
        var forbiddenReferences = typeof(IIntegrationEvent).Assembly
            .GetReferencedAssemblies()
            .Where(reference =>
                reference.Name?.StartsWith("GRD.SpChn.", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    private static T RoundTrip<T>(T integrationEvent)
        where T : IIntegrationEvent
    {
        var json = JsonSerializer.Serialize(integrationEvent, SerializerOptions);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new InvalidOperationException(
                $"The {typeof(T).Name} round-trip returned null.");
    }

    private static void AssertEnvelopeEqual(
        IIntegrationEvent expected,
        IIntegrationEvent actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.EventId, actual.EventId);
        Assert.Equal(expected.OccurredOnUtc, actual.OccurredOnUtc);
        Assert.Equal(DateTimeKind.Utc, actual.OccurredOnUtc.Kind);
    }

    private static void AssertRequiredProperties<T>(
        T integrationEvent,
        params string[] requiredProperties)
        where T : IIntegrationEvent
    {
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(integrationEvent, SerializerOptions));

        foreach (var propertyName in requiredProperties)
        {
            Assert.True(
                document.RootElement.TryGetProperty(propertyName, out _),
                $"The v1 {typeof(T).Name} payload is missing '{propertyName}'.");
        }
    }
}
