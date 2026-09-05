namespace GRD.SpChn.Contracts.IntegrationEvents;

public static class MessagingTopology
{
    public const string OrderExchange = "order.events";
    public const string InventoryExchange = "inventory.events";
    public const string ProcurementExchange = "procurement.events";
    public const string WarehouseExchange = "warehouse.events";
    public const string NotificationExchange = "notification.events";

    public const string OrderPlacedRoutingKey = "order.placed";
    public const string StockReservedRoutingKey = "inventory.stock-reserved";
    public const string StockReservationFailedRoutingKey = "inventory.stock-reservation-failed";
    public const string PurchaseOrderIssuedRoutingKey = "procurement.purchase-order-issued";
    public const string GoodsReceiptPostedRoutingKey = "warehouse.goods-receipt-posted";
    public const string QualityInspectionApprovedRoutingKey = "warehouse.quality-inspection-approved";
    public const string NotificationRequestedRoutingKey = "notification.requested";
}
