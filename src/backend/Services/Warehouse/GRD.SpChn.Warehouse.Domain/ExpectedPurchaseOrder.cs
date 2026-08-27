namespace GRD.SpChn.Warehouse.Domain;

public enum ExpectedPurchaseOrderStatus
{
    Expected,
    Received
}

public sealed record ExpectedPurchaseOrderItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure);

public sealed class ExpectedPurchaseOrder
{
    private readonly IReadOnlyCollection<ExpectedPurchaseOrderItem> _items;

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
        if (Status != ExpectedPurchaseOrderStatus.Expected)
            throw new InvalidOperationException($"Purchase order {PurchaseOrderId} is already {Status}.");
        if (receiverOrganizationUnitId != DestinationOrganizationUnitId)
            throw new UnauthorizedAccessException("The user is not assigned to this receiving location.");
        if (receivedByUserId == Guid.Empty) throw new ArgumentException("A receiving user is required.", nameof(receivedByUserId));
        var receivedByProduct = receivedItems.ToDictionary(item => item.ProductId);
        if (receivedByProduct.Count != Items.Count)
            throw new ArgumentException("Version 1 requires a complete receipt for every PO line.", nameof(receivedItems));
        foreach (var expected in Items)
        {
            if (!receivedByProduct.TryGetValue(expected.ProductId, out var received) ||
                received.Quantity != expected.Quantity ||
                !string.Equals(received.UnitOfMeasure, expected.UnitOfMeasure, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Received product {expected.ProductId} must exactly match the PO quantity and unit.",
                    nameof(receivedItems));
            }
        }

        var now = utcNow ?? DateTime.UtcNow;
        Status = ExpectedPurchaseOrderStatus.Received;
        UpdatedOnUtc = now;
        return GoodsReceipt.Create(
            PurchaseOrderId,
            DestinationOrganizationUnitId,
            receivedByUserId,
            receivedItems,
            now);
    }
}

public sealed record ReceivedItem(Guid ProductId, decimal Quantity, string UnitOfMeasure);
