namespace GRD.SpChn.Inventory.Domain;

public sealed record LocationStockReceipt
{
    private LocationStockReceipt(Guid organizationUnitId, Guid productId, decimal quantity)
    {
        OrganizationUnitId = organizationUnitId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid OrganizationUnitId { get; }
    public Guid ProductId { get; }
    public decimal Quantity { get; }

    public static LocationStockReceipt Create(
        Guid organizationUnitId,
        Guid productId,
        decimal quantity)
    {
        if (organizationUnitId == Guid.Empty) throw new ArgumentException("An inventory location is required.", nameof(organizationUnitId));
        if (productId == Guid.Empty) throw new ArgumentException("A product is required.", nameof(productId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Received quantity must be greater than zero.");
        return new LocationStockReceipt(organizationUnitId, productId, quantity);
    }
}
