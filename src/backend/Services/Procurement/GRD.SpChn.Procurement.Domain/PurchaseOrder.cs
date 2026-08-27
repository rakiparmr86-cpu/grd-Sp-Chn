namespace GRD.SpChn.Procurement.Domain;

public enum PurchaseOrderStatus
{
    Issued,
    Received
}

public sealed record PurchaseOrderItem(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);

public sealed class PurchaseOrder
{
    private readonly IReadOnlyCollection<PurchaseOrderItem> _items;

    private PurchaseOrder(
        Guid id,
        string purchaseOrderNumber,
        Guid materialRequestId,
        Guid supplierId,
        Guid destinationOrganizationUnitId,
        string currency,
        PurchaseOrderStatus status,
        IReadOnlyCollection<PurchaseOrderItem> items,
        DateTime issuedOnUtc,
        DateTime updatedOnUtc)
    {
        Id = id;
        PurchaseOrderNumber = purchaseOrderNumber;
        MaterialRequestId = materialRequestId;
        SupplierId = supplierId;
        DestinationOrganizationUnitId = destinationOrganizationUnitId;
        Currency = currency;
        Status = status;
        _items = items;
        IssuedOnUtc = issuedOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }

    public Guid Id { get; }
    public string PurchaseOrderNumber { get; }
    public Guid MaterialRequestId { get; }
    public Guid SupplierId { get; }
    public Guid DestinationOrganizationUnitId { get; }
    public string Currency { get; }
    public PurchaseOrderStatus Status { get; private set; }
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items;
    public DateTime IssuedOnUtc { get; }
    public DateTime UpdatedOnUtc { get; private set; }

    public static PurchaseOrder Issue(
        MaterialRequest request,
        Guid supplierId,
        string currency,
        IReadOnlyDictionary<Guid, decimal> unitPrices,
        DateTime? utcNow = null)
    {
        if (request.Status != MaterialRequestStatus.Approved)
            throw new InvalidOperationException("Only an approved material request can become a purchase order.");
        if (supplierId == Guid.Empty) throw new ArgumentException("A supplier is required.", nameof(supplierId));
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new ArgumentException("A three-letter currency code is required.", nameof(currency));

        var items = request.Items.Select(item =>
        {
            if (!unitPrices.TryGetValue(item.ProductId, out var unitPrice) || unitPrice <= 0)
                throw new ArgumentException($"A positive unit price is required for product {item.ProductId}.", nameof(unitPrices));
            return new PurchaseOrderItem(item.ProductId, item.Quantity, item.UnitOfMeasure, unitPrice);
        }).ToArray();
        if (unitPrices.Count != items.Length)
            throw new ArgumentException("Prices must match the requested products exactly.", nameof(unitPrices));

        var id = Guid.NewGuid();
        var now = utcNow ?? DateTime.UtcNow;
        return new PurchaseOrder(
            id,
            $"PO-{now:yyyyMMddHHmmss}-{id:N}"[..30],
            request.Id,
            supplierId,
            request.DestinationOrganizationUnitId,
            currency.Trim().ToUpperInvariant(),
            PurchaseOrderStatus.Issued,
            items,
            now,
            now);
    }

    public static PurchaseOrder Rehydrate(
        Guid id,
        string purchaseOrderNumber,
        Guid materialRequestId,
        Guid supplierId,
        Guid destinationOrganizationUnitId,
        string currency,
        PurchaseOrderStatus status,
        IReadOnlyCollection<PurchaseOrderItem> items,
        DateTime issuedOnUtc,
        DateTime updatedOnUtc) =>
        new(id, purchaseOrderNumber, materialRequestId, supplierId,
            destinationOrganizationUnitId, currency, status, items, issuedOnUtc, updatedOnUtc);

    public void MarkReceived(DateTime? utcNow = null)
    {
        if (Status != PurchaseOrderStatus.Issued)
            throw new InvalidOperationException($"Purchase order {Id} cannot be received from {Status}.");
        Status = PurchaseOrderStatus.Received;
        UpdatedOnUtc = utcNow ?? DateTime.UtcNow;
    }
}
