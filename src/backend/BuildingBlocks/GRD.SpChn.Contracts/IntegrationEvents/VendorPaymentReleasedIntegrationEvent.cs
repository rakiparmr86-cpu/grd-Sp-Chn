namespace GRD.SpChn.Contracts.IntegrationEvents;

public sealed record VendorPaymentReleasedIntegrationEvent(
    Guid PaymentId,
    Guid PayableId,
    Guid PurchaseOrderId,
    Guid GoodsReceiptId,
    Guid SupplierId,
    decimal Amount,
    string Currency,
    string BankReference,
    DateTime PaidOnUtc) : IntegrationEvent;
