namespace GRD.SpChn.Contracts.IntegrationEvents;

public static class MessagingTopology
{
    public const string OrderExchange = "order.events";
    public const string InventoryExchange = "inventory.events";

    public const string OrderPlacedRoutingKey = "order.placed";
    public const string StockReservedRoutingKey = "inventory.stock-reserved";
    public const string StockReservationFailedRoutingKey = "inventory.stock-reservation-failed";
}
