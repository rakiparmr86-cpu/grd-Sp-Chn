namespace GRD.SpChn.OrderManagement.Domain;

public sealed class Order
{
    private readonly IReadOnlyCollection<OrderItem> _items;

    private Order(
        Guid id,
        string orderNumber,
        Guid customerId,
        OrderStatus status,
        IReadOnlyCollection<OrderItem> items,
        DateTime createdOnUtc,
        DateTime updatedOnUtc)
    {
        Id = id;
        OrderNumber = orderNumber;
        CustomerId = customerId;
        Status = status;
        _items = items;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }

    public Guid Id { get; }
    public string OrderNumber { get; }
    public Guid CustomerId { get; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items;
    public DateTime CreatedOnUtc { get; }
    public DateTime UpdatedOnUtc { get; private set; }
    public OrderCreatedDomainEvent? CreatedEvent { get; private set; }

    public static Order Create(
        Guid customerId,
        IEnumerable<OrderItem> items,
        DateTime? utcNow = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer id is required.", nameof(customerId));
        }

        var itemList = items?.ToArray()
            ?? throw new ArgumentNullException(nameof(items));
        if (itemList.Length == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        if (itemList.Select(item => item.ProductId).Distinct().Count() != itemList.Length)
        {
            throw new ArgumentException(
                "An order cannot contain duplicate product ids.",
                nameof(items));
        }

        var id = Guid.NewGuid();
        var createdOnUtc = utcNow ?? DateTime.UtcNow;
        var orderNumber = $"ORD-{createdOnUtc:yyyyMMddHHmmss}-{id:N}"[..31];
        var order = new Order(
            id,
            orderNumber,
            customerId,
            OrderStatus.Pending,
            itemList,
            createdOnUtc,
            createdOnUtc);

        order.CreatedEvent = new OrderCreatedDomainEvent(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Items,
            createdOnUtc);

        return order;
    }

    public static Order Rehydrate(
        Guid id,
        string orderNumber,
        Guid customerId,
        OrderStatus status,
        IReadOnlyCollection<OrderItem> items,
        DateTime createdOnUtc,
        DateTime updatedOnUtc) =>
        new(
            id,
            orderNumber,
            customerId,
            status,
            items,
            createdOnUtc,
            updatedOnUtc);

    public void Confirm(DateTime? utcNow = null)
    {
        EnsurePending();
        Status = OrderStatus.Confirmed;
        UpdatedOnUtc = utcNow ?? DateTime.UtcNow;
    }

    public void Cancel(DateTime? utcNow = null)
    {
        EnsurePending();
        Status = OrderStatus.Cancelled;
        UpdatedOnUtc = utcNow ?? DateTime.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Order {Id} cannot transition from {Status}.");
        }
    }
}
