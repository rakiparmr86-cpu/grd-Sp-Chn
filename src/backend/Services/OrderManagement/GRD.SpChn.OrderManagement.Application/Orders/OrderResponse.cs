using GRD.SpChn.OrderManagement.Domain;

namespace GRD.SpChn.OrderManagement.Application.Orders;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string Status,
    IReadOnlyCollection<OrderItemResponse> Items,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc)
{
    public static OrderResponse From(Order order) =>
        new(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Status.ToString(),
            order.Items
                .Select(item => new OrderItemResponse(item.ProductId, item.Quantity))
                .ToArray(),
            order.CreatedOnUtc,
            order.UpdatedOnUtc);
}

public sealed record OrderItemResponse(Guid ProductId, decimal Quantity);
