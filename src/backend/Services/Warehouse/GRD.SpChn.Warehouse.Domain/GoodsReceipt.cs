namespace GRD.SpChn.Warehouse.Domain;

public sealed class GoodsReceipt
{
    private GoodsReceipt(
        Guid id,
        string goodsReceiptNumber,
        Guid purchaseOrderId,
        Guid destinationOrganizationUnitId,
        Guid receivedByUserId,
        IReadOnlyCollection<ReceivedItem> items,
        DateTime receivedOnUtc)
    {
        Id = id;
        GoodsReceiptNumber = goodsReceiptNumber;
        PurchaseOrderId = purchaseOrderId;
        DestinationOrganizationUnitId = destinationOrganizationUnitId;
        ReceivedByUserId = receivedByUserId;
        Items = items;
        ReceivedOnUtc = receivedOnUtc;
    }

    public Guid Id { get; }
    public string GoodsReceiptNumber { get; }
    public Guid PurchaseOrderId { get; }
    public Guid DestinationOrganizationUnitId { get; }
    public Guid ReceivedByUserId { get; }
    public IReadOnlyCollection<ReceivedItem> Items { get; }
    public DateTime ReceivedOnUtc { get; }

    internal static GoodsReceipt Create(
        Guid purchaseOrderId,
        Guid destinationOrganizationUnitId,
        Guid receivedByUserId,
        IReadOnlyCollection<ReceivedItem> items,
        DateTime receivedOnUtc)
    {
        var id = Guid.NewGuid();
        return new GoodsReceipt(
            id,
            $"GRN-{receivedOnUtc:yyyyMMddHHmmss}-{id:N}"[..31],
            purchaseOrderId,
            destinationOrganizationUnitId,
            receivedByUserId,
            items,
            receivedOnUtc);
    }

    public static GoodsReceipt Rehydrate(
        Guid id,
        string goodsReceiptNumber,
        Guid purchaseOrderId,
        Guid destinationOrganizationUnitId,
        Guid receivedByUserId,
        IReadOnlyCollection<ReceivedItem> items,
        DateTime receivedOnUtc) =>
        new(
            id,
            goodsReceiptNumber,
            purchaseOrderId,
            destinationOrganizationUnitId,
            receivedByUserId,
            items,
            receivedOnUtc);
}
