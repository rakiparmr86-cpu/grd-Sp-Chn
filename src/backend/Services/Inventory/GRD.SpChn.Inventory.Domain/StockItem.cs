namespace GRD.SpChn.Inventory.Domain;

public sealed class StockItem
{
    public StockItem(Guid productId, decimal availableQuantity)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A product id is required.", nameof(productId));
        }

        if (availableQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableQuantity),
                "Available quantity cannot be negative.");
        }

        ProductId = productId;
        AvailableQuantity = availableQuantity;
    }

    public Guid ProductId { get; }
    public decimal AvailableQuantity { get; private set; }

    public bool CanReserve(decimal quantity) =>
        quantity > 0 && AvailableQuantity >= quantity;

    public void Reserve(decimal quantity)
    {
        if (!CanReserve(quantity))
        {
            throw new InvalidOperationException(
                $"Product {ProductId} does not have {quantity} units available.");
        }

        AvailableQuantity -= quantity;
    }
}
