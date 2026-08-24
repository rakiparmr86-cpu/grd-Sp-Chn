using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.ContractTests;

public sealed class IntegrationEventContractTests
{
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
        Assert.Equal(DateTimeKind.Utc, first.OccurredOnUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, second.OccurredOnUtc.Kind);
    }
}
