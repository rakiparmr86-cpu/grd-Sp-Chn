namespace GRD.SpChn.Procurement.Domain;

public sealed class PurchaseOrderDispatch
{
    private PurchaseOrderDispatch(
        Guid id,
        Guid purchaseOrderId,
        Guid supplierId,
        Guid recordedByUserId,
        string vendorDispatchReference,
        string? deliveryChallanNumber,
        string? transporterName,
        string? vehicleNumber,
        DateTime dispatchedOnUtc,
        DateTime? expectedDeliveryOnUtc,
        string? notes,
        DateTime recordedOnUtc)
    {
        Id = id;
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        RecordedByUserId = recordedByUserId;
        VendorDispatchReference = vendorDispatchReference;
        DeliveryChallanNumber = deliveryChallanNumber;
        TransporterName = transporterName;
        VehicleNumber = vehicleNumber;
        DispatchedOnUtc = dispatchedOnUtc;
        ExpectedDeliveryOnUtc = expectedDeliveryOnUtc;
        Notes = notes;
        RecordedOnUtc = recordedOnUtc;
    }

    public Guid Id { get; }
    public Guid PurchaseOrderId { get; }
    public Guid SupplierId { get; }
    public Guid RecordedByUserId { get; }
    public string VendorDispatchReference { get; }
    public string? DeliveryChallanNumber { get; }
    public string? TransporterName { get; }
    public string? VehicleNumber { get; }
    public DateTime DispatchedOnUtc { get; }
    public DateTime? ExpectedDeliveryOnUtc { get; }
    public string? Notes { get; }
    public DateTime RecordedOnUtc { get; }

    public static PurchaseOrderDispatch Record(
        PurchaseOrder purchaseOrder,
        Guid recordedByUserId,
        string vendorDispatchReference,
        string? deliveryChallanNumber,
        string? transporterName,
        string? vehicleNumber,
        DateTime dispatchedOnUtc,
        DateTime? expectedDeliveryOnUtc,
        string? notes,
        DateTime? utcNow = null)
    {
        if (purchaseOrder.Status != PurchaseOrderStatus.Issued)
            throw new InvalidOperationException("Only an issued purchase order can be dispatched.");
        if (recordedByUserId == Guid.Empty)
            throw new ArgumentException("The internal user recording dispatch is required.", nameof(recordedByUserId));
        if (string.IsNullOrWhiteSpace(vendorDispatchReference))
            throw new ArgumentException("Vendor dispatch reference is required.", nameof(vendorDispatchReference));

        var normalizedDispatchDate = DateTime.SpecifyKind(dispatchedOnUtc, DateTimeKind.Utc);
        DateTime? normalizedExpectedDate = expectedDeliveryOnUtc is null
            ? null
            : DateTime.SpecifyKind(expectedDeliveryOnUtc.Value, DateTimeKind.Utc);
        if (normalizedExpectedDate < normalizedDispatchDate)
            throw new ArgumentException("Expected delivery cannot be earlier than dispatch.", nameof(expectedDeliveryOnUtc));

        var recordedOnUtc = utcNow ?? DateTime.UtcNow;
        if (normalizedDispatchDate > recordedOnUtc.AddHours(24))
            throw new ArgumentException("Dispatch date cannot be more than one day in the future.", nameof(dispatchedOnUtc));

        return new PurchaseOrderDispatch(
            Guid.NewGuid(),
            purchaseOrder.Id,
            purchaseOrder.SupplierId,
            recordedByUserId,
            vendorDispatchReference.Trim(),
            Normalize(deliveryChallanNumber),
            Normalize(transporterName),
            Normalize(vehicleNumber),
            normalizedDispatchDate,
            normalizedExpectedDate,
            Normalize(notes),
            recordedOnUtc);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
