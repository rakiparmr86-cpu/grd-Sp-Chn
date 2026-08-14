using FluentValidation;
using GRD.SpChn.Inventory.Domain;
using GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;
using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.UnitTests;

public sealed class OrderInventoryWorkflowTests
{
    [Fact]
    public void New_order_is_pending_and_raises_creation_event()
    {
        var customerId = Guid.NewGuid();
        var item = OrderItem.Create(Guid.NewGuid(), 2);
        var now = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

        var order = Order.Create(customerId, [item], now);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.NotNull(order.CreatedEvent);
        Assert.Equal(order.Id, order.CreatedEvent.OrderId);
        Assert.Equal(now, order.CreatedOnUtc);
    }

    [Fact]
    public void Pending_order_can_be_confirmed_only_once()
    {
        var order = Order.Create(
            Guid.NewGuid(),
            [OrderItem.Create(Guid.NewGuid(), 1)]);

        order.Confirm();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }

    [Fact]
    public void Stock_reservation_never_allows_negative_available_quantity()
    {
        var stock = new StockItem(Guid.NewGuid(), 5);

        stock.Reserve(3);

        Assert.Equal(2, stock.AvailableQuantity);
        Assert.Throws<InvalidOperationException>(() => stock.Reserve(3));
    }

    [Fact]
    public void Create_order_validation_rejects_duplicate_products()
    {
        var productId = Guid.NewGuid();
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            [
                new CreateOrderItem(productId, 1),
                new CreateOrderItem(productId, 2)
            ]);

        var result = new CreateOrderCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains("duplicate product ids"));
    }
}
