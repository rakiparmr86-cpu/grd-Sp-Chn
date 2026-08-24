using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Inventory.Application.Stock.ReserveStock;
using GRD.SpChn.Inventory.Domain;
using GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;
using GRD.SpChn.OrderManagement.Application.Orders.GetOrder;
using GRD.SpChn.OrderManagement.Domain;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using InventoryInboxStore = GRD.SpChn.Inventory.Application.Abstractions.IInboxStore;
using InventoryOutboxWriter = GRD.SpChn.Inventory.Application.Abstractions.IOutboxWriter;
using InventoryRepository = GRD.SpChn.Inventory.Application.Abstractions.IInventoryRepository;
using InventoryUnitOfWork = GRD.SpChn.Inventory.Application.Abstractions.IUnitOfWork;
using OrderOutboxWriter = GRD.SpChn.OrderManagement.Application.Abstractions.IOutboxWriter;
using OrderRepository = GRD.SpChn.OrderManagement.Application.Abstractions.IOrderRepository;
using OrderUnitOfWork = GRD.SpChn.OrderManagement.Application.Abstractions.IUnitOfWork;

namespace GRD.SpChn.UnitTests;

public sealed class ApplicationHandlerTests
{
    [Fact]
    public async Task Create_order_persists_pending_order_and_outbox_through_ports()
    {
        var unitOfWork = new FakeOrderUnitOfWork();
        var repository = new FakeOrderRepository();
        var outbox = new FakeOrderOutboxWriter();
        using var provider = BuildOrderProvider(unitOfWork, repository, outbox);

        var productId = Guid.NewGuid();
        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateOrderCommand(
                Guid.NewGuid(),
                [new CreateOrderItem(productId, 2)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Pending.ToString(), result.Value.Status);
        Assert.NotNull(repository.Stored);
        Assert.Equal(OrderStatus.Pending, repository.Stored.Status);
        var message = Assert.Single(outbox.Messages);
        var orderPlaced = Assert.IsType<OrderPlacedIntegrationEvent>(message.Event);
        Assert.Equal(repository.Stored.Id, orderPlaced.OrderId);
        Assert.Equal(MessagingTopology.OrderExchange, message.Exchange);
        Assert.Equal(MessagingTopology.OrderPlacedRoutingKey, message.RoutingKey);
        Assert.Equal(1, unitOfWork.ExecutionCount);
    }

    [Fact]
    public async Task Get_order_query_reads_status_without_opening_a_transaction()
    {
        var unitOfWork = new FakeOrderUnitOfWork();
        var repository = new FakeOrderRepository
        {
            Stored = Order.Create(
                Guid.NewGuid(),
                [OrderItem.Create(Guid.NewGuid(), 1)])
        };
        using var provider = BuildOrderProvider(
            unitOfWork,
            repository,
            new FakeOrderOutboxWriter());

        var result = await provider.GetRequiredService<ISender>().Send(
            new GetOrderQuery(repository.Stored.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Pending.ToString(), result.Value.Status);
        Assert.Equal(0, unitOfWork.ExecutionCount);
    }

    [Fact]
    public async Task Invalid_create_order_stops_before_transaction_and_handler()
    {
        var unitOfWork = new FakeOrderUnitOfWork();
        var repository = new FakeOrderRepository();
        var outbox = new FakeOrderOutboxWriter();
        using var provider = BuildOrderProvider(unitOfWork, repository, outbox);

        var result = await provider.GetRequiredService<ISender>().Send(
            new CreateOrderCommand(Guid.Empty, []));

        Assert.True(result.IsFailure);
        Assert.All(
            result.Errors,
            error => Assert.Equal(GRD.SpChn.SharedKernel.ErrorType.Validation, error.Type));
        Assert.Equal(0, unitOfWork.ExecutionCount);
        Assert.Null(repository.Stored);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Reserve_stock_updates_all_items_and_writes_success_outbox()
    {
        var productId = Guid.NewGuid();
        var unitOfWork = new FakeInventoryUnitOfWork();
        var inbox = new FakeInventoryInboxStore();
        var repository = new FakeInventoryRepository(
            new StockItem(productId, 10));
        var outbox = new FakeInventoryOutboxWriter();
        using var provider = BuildInventoryProvider(
            unitOfWork,
            inbox,
            repository,
            outbox);

        var orderId = Guid.NewGuid();
        var result = await provider.GetRequiredService<ISender>().Send(
            new ReserveStockCommand(
                Guid.NewGuid(),
                orderId,
                [new ReserveStockItem(productId, 2)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationOutcome.Reserved, result.Value.Outcome);
        Assert.Equal(8, repository.Items[productId].AvailableQuantity);
        Assert.Equal(1, repository.UpdateCount);
        var message = Assert.Single(outbox.Messages);
        var stockReserved = Assert.IsType<StockReservedIntegrationEvent>(message.Event);
        Assert.Equal(orderId, stockReserved.OrderId);
        Assert.Equal(MessagingTopology.InventoryExchange, message.Exchange);
        Assert.Equal(MessagingTopology.StockReservedRoutingKey, message.RoutingKey);
        Assert.Equal(1, unitOfWork.ExecutionCount);
    }

    [Fact]
    public async Task Reserve_stock_rejects_entire_multi_item_request_without_partial_update()
    {
        var availableProductId = Guid.NewGuid();
        var insufficientProductId = Guid.NewGuid();
        var unitOfWork = new FakeInventoryUnitOfWork();
        var repository = new FakeInventoryRepository(
            new StockItem(availableProductId, 10),
            new StockItem(insufficientProductId, 1));
        var outbox = new FakeInventoryOutboxWriter();
        using var provider = BuildInventoryProvider(
            unitOfWork,
            new FakeInventoryInboxStore(),
            repository,
            outbox);

        var result = await provider.GetRequiredService<ISender>().Send(
            new ReserveStockCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [
                    new ReserveStockItem(availableProductId, 2),
                    new ReserveStockItem(insufficientProductId, 5)
                ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationOutcome.Rejected, result.Value.Outcome);
        Assert.Contains("were requested", result.Value.FailureReason);
        Assert.Equal(10, repository.Items[availableProductId].AvailableQuantity);
        Assert.Equal(1, repository.Items[insufficientProductId].AvailableQuantity);
        Assert.Equal(0, repository.UpdateCount);
        var message = Assert.Single(outbox.Messages);
        Assert.IsType<StockReservationFailedIntegrationEvent>(message.Event);
        Assert.Equal(
            MessagingTopology.StockReservationFailedRoutingKey,
            message.RoutingKey);
    }

    [Fact]
    public async Task Duplicate_order_placed_event_does_not_reserve_or_publish_again()
    {
        var eventId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var inbox = new FakeInventoryInboxStore();
        inbox.ProcessedEventIds.Add(eventId);
        var repository = new FakeInventoryRepository(
            new StockItem(productId, 10));
        var outbox = new FakeInventoryOutboxWriter();
        using var provider = BuildInventoryProvider(
            new FakeInventoryUnitOfWork(),
            inbox,
            repository,
            outbox);

        var result = await provider.GetRequiredService<ISender>().Send(
            new ReserveStockCommand(
                eventId,
                Guid.NewGuid(),
                [new ReserveStockItem(productId, 2)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationOutcome.Duplicate, result.Value.Outcome);
        Assert.Equal(10, repository.Items[productId].AvailableQuantity);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task Invalid_reservation_stops_before_transaction_and_ports()
    {
        var unitOfWork = new FakeInventoryUnitOfWork();
        var inbox = new FakeInventoryInboxStore();
        var repository = new FakeInventoryRepository();
        var outbox = new FakeInventoryOutboxWriter();
        using var provider = BuildInventoryProvider(
            unitOfWork,
            inbox,
            repository,
            outbox);

        var result = await provider.GetRequiredService<ISender>().Send(
            new ReserveStockCommand(Guid.Empty, Guid.Empty, []));

        Assert.True(result.IsFailure);
        Assert.Equal(0, unitOfWork.ExecutionCount);
        Assert.Empty(inbox.ProcessedEventIds);
        Assert.Empty(repository.Items);
        Assert.Empty(outbox.Messages);
    }

    private static ServiceProvider BuildOrderProvider(
        OrderUnitOfWork unitOfWork,
        OrderRepository repository,
        OrderOutboxWriter outboxWriter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        GRD.SpChn.OrderManagement.Application.DependencyInjection.AddApplication(services);
        services.AddScoped(_ => unitOfWork);
        services.AddScoped(_ => repository);
        services.AddScoped(_ => outboxWriter);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildInventoryProvider(
        InventoryUnitOfWork unitOfWork,
        InventoryInboxStore inboxStore,
        InventoryRepository repository,
        InventoryOutboxWriter outboxWriter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        GRD.SpChn.Inventory.Application.DependencyInjection.AddApplication(services);
        services.AddScoped(_ => unitOfWork);
        services.AddScoped(_ => inboxStore);
        services.AddScoped(_ => repository);
        services.AddScoped(_ => outboxWriter);
        return services.BuildServiceProvider();
    }

    private sealed class FakeOrderUnitOfWork : OrderUnitOfWork
    {
        public int ExecutionCount { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return await operation(cancellationToken);
        }
    }

    private sealed class FakeOrderRepository : OrderRepository
    {
        public Order? Stored { get; set; }

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            Stored = order;
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored?.Id == orderId ? Stored : null);

        public Task<Order?> GetByIdForUpdateAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            GetByIdAsync(orderId, cancellationToken);

        public Task UpdateAsync(
            Order order,
            CancellationToken cancellationToken = default)
        {
            Stored = order;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrderOutboxWriter : OrderOutboxWriter
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task AddAsync(
            IIntegrationEvent integrationEvent,
            string exchangeName,
            string routingKey,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new OutboxMessage(integrationEvent, exchangeName, routingKey));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInventoryUnitOfWork : InventoryUnitOfWork
    {
        public int ExecutionCount { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return await operation(cancellationToken);
        }
    }

    private sealed class FakeInventoryInboxStore : InventoryInboxStore
    {
        public HashSet<Guid> ProcessedEventIds { get; } = [];

        public Task<bool> TryAddAsync(
            Guid eventId,
            string eventType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessedEventIds.Add(eventId));
    }

    private sealed class FakeInventoryRepository : InventoryRepository
    {
        public FakeInventoryRepository(params StockItem[] items)
        {
            Items = items.ToDictionary(item => item.ProductId);
        }

        public Dictionary<Guid, StockItem> Items { get; }
        public int UpdateCount { get; private set; }

        public Task UpsertAsync(
            StockItem stock,
            CancellationToken cancellationToken = default)
        {
            Items[stock.ProductId] = stock;
            return Task.CompletedTask;
        }

        public Task<StockItem?> GetByProductIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(productId));

        public Task<StockItem?> GetByProductIdForUpdateAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            GetByProductIdAsync(productId, cancellationToken);

        public Task UpdateAsync(
            StockItem stock,
            CancellationToken cancellationToken = default)
        {
            Items[stock.ProductId] = stock;
            UpdateCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInventoryOutboxWriter : InventoryOutboxWriter
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task AddAsync(
            IIntegrationEvent integrationEvent,
            string exchangeName,
            string routingKey,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new OutboxMessage(integrationEvent, exchangeName, routingKey));
            return Task.CompletedTask;
        }
    }

    private sealed record OutboxMessage(
        IIntegrationEvent Event,
        string Exchange,
        string RoutingKey);
}
