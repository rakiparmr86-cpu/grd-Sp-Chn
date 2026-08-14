namespace GRD.SpChn.OrderManagement.Domain;

public sealed record OrderItem(Guid ProductId, decimal Quantity)
{
    public static OrderItem Create(Guid productId, decimal quantity)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A product id is required.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Order item quantity must be greater than zero.");
        }

        return new OrderItem(productId, quantity);
    }
}
