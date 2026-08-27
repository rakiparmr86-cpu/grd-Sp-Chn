namespace GRD.SpChn.Procurement.Domain;

public sealed class MaterialRequest
{
    private readonly IReadOnlyCollection<MaterialRequestItem> _items;

    private MaterialRequest(
        Guid id,
        string requestNumber,
        Guid requestingOrganizationUnitId,
        Guid destinationOrganizationUnitId,
        Guid requestedByUserId,
        string purpose,
        MaterialRequestStatus status,
        IReadOnlyCollection<MaterialRequestItem> items,
        Guid? approvedByUserId,
        Guid? purchaseOrderId,
        DateTime createdOnUtc,
        DateTime updatedOnUtc)
    {
        Id = id;
        RequestNumber = requestNumber;
        RequestingOrganizationUnitId = requestingOrganizationUnitId;
        DestinationOrganizationUnitId = destinationOrganizationUnitId;
        RequestedByUserId = requestedByUserId;
        Purpose = purpose;
        Status = status;
        _items = items;
        ApprovedByUserId = approvedByUserId;
        PurchaseOrderId = purchaseOrderId;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }

    public Guid Id { get; }
    public string RequestNumber { get; }
    public Guid RequestingOrganizationUnitId { get; }
    public Guid DestinationOrganizationUnitId { get; }
    public Guid RequestedByUserId { get; }
    public string Purpose { get; }
    public MaterialRequestStatus Status { get; private set; }
    public IReadOnlyCollection<MaterialRequestItem> Items => _items;
    public Guid? ApprovedByUserId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public DateTime CreatedOnUtc { get; }
    public DateTime UpdatedOnUtc { get; private set; }

    public static MaterialRequest Create(
        Guid requestingOrganizationUnitId,
        Guid destinationOrganizationUnitId,
        Guid requestedByUserId,
        string purpose,
        IEnumerable<MaterialRequestItem> items,
        DateTime? utcNow = null)
    {
        if (requestingOrganizationUnitId == Guid.Empty) throw new ArgumentException("A requesting organization unit is required.", nameof(requestingOrganizationUnitId));
        if (destinationOrganizationUnitId == Guid.Empty) throw new ArgumentException("A destination organization unit is required.", nameof(destinationOrganizationUnitId));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("A requesting user is required.", nameof(requestedByUserId));
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("A business purpose is required.", nameof(purpose));
        var lines = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        if (lines.Length == 0) throw new ArgumentException("At least one requested material is required.", nameof(items));
        if (lines.Select(item => item.ProductId).Distinct().Count() != lines.Length)
            throw new ArgumentException("Duplicate products are not allowed.", nameof(items));

        var id = Guid.NewGuid();
        var now = utcNow ?? DateTime.UtcNow;
        return new MaterialRequest(
            id,
            $"MR-{now:yyyyMMddHHmmss}-{id:N}"[..30],
            requestingOrganizationUnitId,
            destinationOrganizationUnitId,
            requestedByUserId,
            purpose.Trim(),
            MaterialRequestStatus.Submitted,
            lines,
            null,
            null,
            now,
            now);
    }

    public static MaterialRequest Rehydrate(
        Guid id,
        string requestNumber,
        Guid requestingOrganizationUnitId,
        Guid destinationOrganizationUnitId,
        Guid requestedByUserId,
        string purpose,
        MaterialRequestStatus status,
        IReadOnlyCollection<MaterialRequestItem> items,
        Guid? approvedByUserId,
        Guid? purchaseOrderId,
        DateTime createdOnUtc,
        DateTime updatedOnUtc) =>
        new(id, requestNumber, requestingOrganizationUnitId, destinationOrganizationUnitId,
            requestedByUserId, purpose, status, items, approvedByUserId, purchaseOrderId,
            createdOnUtc, updatedOnUtc);

    public void Approve(Guid approvedByUserId, DateTime? utcNow = null)
    {
        if (Status != MaterialRequestStatus.Submitted)
            throw new InvalidOperationException($"Request {Id} cannot be approved from {Status}.");
        if (approvedByUserId == Guid.Empty) throw new ArgumentException("An approver is required.", nameof(approvedByUserId));
        ApprovedByUserId = approvedByUserId;
        Status = MaterialRequestStatus.Approved;
        UpdatedOnUtc = utcNow ?? DateTime.UtcNow;
    }

    public void AttachPurchaseOrder(Guid purchaseOrderId, DateTime? utcNow = null)
    {
        if (Status != MaterialRequestStatus.Approved)
            throw new InvalidOperationException($"Request {Id} cannot create a PO from {Status}.");
        PurchaseOrderId = purchaseOrderId;
        Status = MaterialRequestStatus.PurchaseOrderIssued;
        UpdatedOnUtc = utcNow ?? DateTime.UtcNow;
    }

    public void MarkReceived(DateTime? utcNow = null)
    {
        if (Status != MaterialRequestStatus.PurchaseOrderIssued)
            throw new InvalidOperationException($"Request {Id} cannot be received from {Status}.");
        Status = MaterialRequestStatus.Received;
        UpdatedOnUtc = utcNow ?? DateTime.UtcNow;
    }
}
