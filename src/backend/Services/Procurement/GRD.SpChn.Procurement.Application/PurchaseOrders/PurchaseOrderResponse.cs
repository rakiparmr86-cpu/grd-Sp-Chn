using GRD.SpChn.Procurement.Domain;

namespace GRD.SpChn.Procurement.Application.PurchaseOrders;

public sealed record PurchaseOrderResponse(
    Guid Id,
    string PurchaseOrderNumber,
    Guid MaterialRequestId,
    Guid SupplierId,
    Guid DestinationOrganizationUnitId,
    string Currency,
    string Status,
    IReadOnlyCollection<PurchaseOrderItemResponse> Items,
    DateTime IssuedOnUtc,
    DateTime? DispatchedOnUtc,
    DateTime UpdatedOnUtc)
{
    public static PurchaseOrderResponse From(PurchaseOrder order) =>
        new(
            order.Id,
            order.PurchaseOrderNumber,
            order.MaterialRequestId,
            order.SupplierId,
            order.DestinationOrganizationUnitId,
            order.Currency,
            order.Status.ToString(),
            order.Items.Select(item => new PurchaseOrderItemResponse(
                item.ProductId,
                item.Quantity,
                item.UnitOfMeasure,
                item.UnitPrice)).ToArray(),
            order.IssuedOnUtc,
            order.DispatchedOnUtc,
            order.UpdatedOnUtc);
}

public sealed record PurchaseOrderItemResponse(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
