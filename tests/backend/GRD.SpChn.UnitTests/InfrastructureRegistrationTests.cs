using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.EventBus.Abstractions;
using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Persistence.MySql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.UnitTests;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void Event_bus_registration_is_lazy_and_resolvable_without_a_broker()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddLogging();
        services.AddRabbitMqEventBus(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public async Task Missing_database_connection_string_fails_when_connection_is_requested()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddMySqlPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbConnectionFactory>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.OpenConnectionAsync().AsTask());

        Assert.Contains("ConnectionStrings__Database", exception.Message);
    }

    [Fact]
    public void Integration_events_receive_unique_identity_and_utc_timestamps()
    {
        var first = new SalesOrderCreatedIntegrationEvent(
            Guid.NewGuid(),
            "SO-001",
            Guid.NewGuid());
        var second = new SalesOrderCreatedIntegrationEvent(
            Guid.NewGuid(),
            "SO-002",
            Guid.NewGuid());

        Assert.NotEqual(first.EventId, second.EventId);
        Assert.Equal(DateTimeKind.Utc, first.OccurredOnUtc.Kind);
    }
}
