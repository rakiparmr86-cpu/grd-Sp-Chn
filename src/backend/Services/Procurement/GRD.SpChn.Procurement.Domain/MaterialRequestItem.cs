namespace GRD.SpChn.Procurement.Domain;

public sealed record MaterialRequestItem
{
    private MaterialRequestItem(Guid productId, decimal quantity, string unitOfMeasure)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
    }

    public Guid ProductId { get; }
    public decimal Quantity { get; }
    public string UnitOfMeasure { get; }

    public static MaterialRequestItem Create(
        Guid productId,
        decimal quantity,
        string unitOfMeasure)
    {
        if (productId == Guid.Empty) throw new ArgumentException("A product id is required.", nameof(productId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(unitOfMeasure)) throw new ArgumentException("A unit of measure is required.", nameof(unitOfMeasure));
        return new MaterialRequestItem(productId, quantity, unitOfMeasure.Trim().ToUpperInvariant());
    }
}
