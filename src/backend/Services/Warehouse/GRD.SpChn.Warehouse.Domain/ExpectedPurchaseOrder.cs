namespace GRD.SpChn.Warehouse.Domain;

public enum ExpectedPurchaseOrderStatus
{
    Expected,
    PartiallyReceived,
    Received
}

public sealed record ExpectedPurchaseOrderItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure,
    decimal ReceivedQuantity = 0)
{
    public decimal RemainingQuantity => Quantity - ReceivedQuantity;
}

public sealed class ExpectedPurchaseOrder
{
    private IReadOnlyCollection<ExpectedPurchaseOrderItem> _items;

    private ExpectedPurchaseOrder(
        Guid purchaseOrderId,
        string purchaseOrderNumber,
        Guid supplierId,
        Guid destinationOrganizationUnitId,
        ExpectedPurchaseOrderStatus status,
        IReadOnlyCollection<ExpectedPurchaseOrderItem> items,
        DateTime issuedOnUtc,
        DateTime updatedOnUtc)
    {
        PurchaseOrderId = purchaseOrderId;
        PurchaseOrderNumber = purchaseOrderNumber;
        SupplierId = supplierId;
        DestinationOrganizationUnitId = destinationOrganizationUnitId;
        Status = status;
        _items = items;
        IssuedOnUtc = issuedOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }

    public Guid PurchaseOrderId { get; }
    public string PurchaseOrderNumber { get; }
    public Guid SupplierId { get; }
    public Guid DestinationOrganizationUnitId { get; }
    public ExpectedPurchaseOrderStatus Status { get; private set; }
    public IReadOnlyCollection<ExpectedPurchaseOrderItem> Items => _items;
    public DateTime IssuedOnUtc { get; }
    public DateTime UpdatedOnUtc { get; private set; }

    public static ExpectedPurchaseOrder Register(
        Guid purchaseOrderId,
        string purchaseOrderNumber,
        Guid supplierId,
        Guid destinationOrganizationUnitId,
        IEnumerable<ExpectedPurchaseOrderItem> items,
        DateTime issuedOnUtc)
    {
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("A purchase order id is required.", nameof(purchaseOrderId));
        if (string.IsNullOrWhiteSpace(purchaseOrderNumber)) throw new ArgumentException("A purchase order number is required.", nameof(purchaseOrderNumber));
        if (supplierId == Guid.Empty) throw new ArgumentException("A supplier id is required.", nameof(supplierId));
        if (destinationOrganizationUnitId == Guid.Empty) throw new ArgumentException("A destination organization unit is required.", nameof(destinationOrganizationUnitId));
        var lines = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        if (lines.Length == 0) throw new ArgumentException("The purchase order requires items.", nameof(items));
        return new ExpectedPurchaseOrder(
            purchaseOrderId,
            purchaseOrderNumber,
            supplierId,
            destinationOrganizationUnitId,
            ExpectedPurchaseOrderStatus.Expected,
            lines,
            issuedOnUtc,
            issuedOnUtc);
    }

    public static ExpectedPurchaseOrder Rehydrate(
        Guid purchaseOrderId,
        string purchaseOrderNumber,
        Guid supplierId,
        Guid destinationOrganizationUnitId,
        ExpectedPurchaseOrderStatus status,
        IReadOnlyCollection<ExpectedPurchaseOrderItem> items,
        DateTime issuedOnUtc,
        DateTime updatedOnUtc) =>
        new(purchaseOrderId, purchaseOrderNumber, supplierId,
            destinationOrganizationUnitId, status, items, issuedOnUtc, updatedOnUtc);

    public GoodsReceipt Receive(
        Guid receiverOrganizationUnitId,
        Guid receivedByUserId,
        IReadOnlyCollection<ReceivedItem> receivedItems,
        DateTime? utcNow = null)
    {
        if (Status == ExpectedPurchaseOrderStatus.Received)
            throw new InvalidOperationException($"Purchase order {PurchaseOrderId} is already {Status}.");
        if (receiverOrganizationUnitId != DestinationOrganizationUnitId)
            throw new UnauthorizedAccessException("The user is not assigned to this receiving location.");
        if (receivedByUserId == Guid.Empty) throw new ArgumentException("A receiving user is required.", nameof(receivedByUserId));
        if (receivedItems is null || receivedItems.Count == 0)
            throw new ArgumentException("Enter at least one received quantity.", nameof(receivedItems));
        if (receivedItems.Select(item => item.ProductId).Distinct().Count() != receivedItems.Count)
            throw new ArgumentException("A product can appear only once in a goods receipt.", nameof(receivedItems));

        var expectedByProduct = Items.ToDictionary(item => item.ProductId);
        foreach (var received in receivedItems)
        {
            if (!expectedByProduct.TryGetValue(received.ProductId, out var expected))
                throw new ArgumentException($"Product {received.ProductId} is not on this purchase order.", nameof(receivedItems));
            if (received.Quantity <= 0)
                throw new ArgumentException($"Received quantity for product {received.ProductId} must be greater than zero.", nameof(receivedItems));
            if (!string.Equals(received.UnitOfMeasure, expected.UnitOfMeasure, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Received product {expected.ProductId} must use PO unit {expected.UnitOfMeasure}.",
                    nameof(receivedItems));
            if (received.Quantity > expected.RemainingQuantity)
                throw new ArgumentException(
                    $"Received quantity {received.Quantity} for product {received.ProductId} exceeds remaining PO quantity {expected.RemainingQuantity}.",
                    nameof(receivedItems));
        }

        var now = utcNow ?? DateTime.UtcNow;
        var receivedByProduct = receivedItems.ToDictionary(item => item.ProductId);
        _items = Items.Select(item => receivedByProduct.TryGetValue(item.ProductId, out var received)
                ? item with { ReceivedQuantity = item.ReceivedQuantity + received.Quantity }
                : item)
            .ToArray();
        var completesPurchaseOrder = Items.All(item => item.RemainingQuantity == 0);
        Status = completesPurchaseOrder
            ? ExpectedPurchaseOrderStatus.Received
            : ExpectedPurchaseOrderStatus.PartiallyReceived;
        UpdatedOnUtc = now;
        return GoodsReceipt.Create(
            PurchaseOrderId,
            DestinationOrganizationUnitId,
            receivedByUserId,
            receivedItems,
            completesPurchaseOrder,
            now);
    }
}

public sealed record ReceivedItem(Guid ProductId, decimal Quantity, string UnitOfMeasure);
