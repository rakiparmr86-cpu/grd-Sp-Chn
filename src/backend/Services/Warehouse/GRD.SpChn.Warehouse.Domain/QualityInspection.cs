namespace GRD.SpChn.Warehouse.Domain;

public enum QualityInspectionResult
{
    Passed,
    Rejected
}

public sealed class QualityInspection
{
    private QualityInspection(
        Guid id,
        Guid goodsReceiptId,
        Guid purchaseOrderId,
        Guid destinationOrganizationUnitId,
        Guid inspectedByUserId,
        QualityInspectionResult result,
        string? notes,
        DateTime inspectedOnUtc)
    {
        Id = id;
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderId = purchaseOrderId;
        DestinationOrganizationUnitId = destinationOrganizationUnitId;
        InspectedByUserId = inspectedByUserId;
        Result = result;
        Notes = notes;
        InspectedOnUtc = inspectedOnUtc;
    }

    public Guid Id { get; }
    public Guid GoodsReceiptId { get; }
    public Guid PurchaseOrderId { get; }
    public Guid DestinationOrganizationUnitId { get; }
    public Guid InspectedByUserId { get; }
    public QualityInspectionResult Result { get; }
    public string? Notes { get; }
    public DateTime InspectedOnUtc { get; }

    public static QualityInspection Complete(
        GoodsReceipt receipt,
        Guid inspectorOrganizationUnitId,
        Guid inspectedByUserId,
        QualityInspectionResult result,
        string? notes,
        DateTime? utcNow = null)
    {
        if (inspectorOrganizationUnitId != receipt.DestinationOrganizationUnitId)
            throw new UnauthorizedAccessException("Quality inspection must be performed at the receiving location.");
        if (inspectedByUserId == Guid.Empty)
            throw new ArgumentException("A quality inspector user is required.", nameof(inspectedByUserId));
        if (result == QualityInspectionResult.Rejected && string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("A rejection reason is required when quality fails.", nameof(notes));

        return new QualityInspection(
            Guid.NewGuid(),
            receipt.Id,
            receipt.PurchaseOrderId,
            receipt.DestinationOrganizationUnitId,
            inspectedByUserId,
            result,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            utcNow ?? DateTime.UtcNow);
    }

    public static QualityInspection Rehydrate(
        Guid id,
        Guid goodsReceiptId,
        Guid purchaseOrderId,
        Guid destinationOrganizationUnitId,
        Guid inspectedByUserId,
        QualityInspectionResult result,
        string? notes,
        DateTime inspectedOnUtc) =>
        new(
            id,
            goodsReceiptId,
            purchaseOrderId,
            destinationOrganizationUnitId,
            inspectedByUserId,
            result,
            notes,
            inspectedOnUtc);
}
