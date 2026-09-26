namespace GRD.SpChn.Accounting.Application.Payables;

public sealed record InvoiceMatchItem(
    Guid ProductId,
    decimal PurchaseOrderQuantity,
    decimal AcceptedQuantity,
    string UnitOfMeasure,
    decimal PurchaseOrderUnitPrice);

public sealed record InvoiceCandidateItem(
    Guid ProductId,
    decimal AcceptedQuantity,
    decimal AlreadyInvoicedQuantity,
    decimal RemainingQuantity,
    string UnitOfMeasure,
    decimal PurchaseOrderUnitPrice);

public sealed record InvoiceCandidateResponse(
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid GoodsReceiptId,
    string GoodsReceiptNumber,
    Guid SupplierId,
    string Currency,
    DateTime AcceptedOnUtc,
    IReadOnlyCollection<InvoiceCandidateItem> Items);

public sealed record InvoiceMatchContext(
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid GoodsReceiptId,
    string GoodsReceiptNumber,
    Guid SupplierId,
    string Currency,
    IReadOnlyCollection<InvoiceMatchItem> Items);

public sealed record InvoiceLineInput(Guid ProductId, decimal Quantity, decimal UnitPrice);

public sealed record PayableResponse(
    Guid Id,
    string InvoiceNumber,
    string SupplierInvoiceNumber,
    Guid PurchaseOrderId,
    Guid GoodsReceiptId,
    Guid SupplierId,
    string Currency,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    string Status,
    Guid CreatedByUserId,
    Guid? ApprovedByUserId,
    Guid? PaidByUserId,
    string? BankReference,
    DateTime InvoiceDateUtc,
    DateTime DueDateUtc,
    DateTime CreatedOnUtc,
    DateTime? ApprovedOnUtc,
    DateTime? PaidOnUtc);

public sealed record JournalEntryResponse(
    Guid Id,
    string EntryNumber,
    string EntryType,
    string SourceType,
    Guid SourceId,
    string Currency,
    decimal DebitTotal,
    decimal CreditTotal,
    string Description,
    DateTime PostedOnUtc);

public sealed record PayableDetailLine(
    Guid ProductId,
    string UnitOfMeasure,
    decimal? PurchaseOrderQuantity,
    decimal? AcceptedQuantity,
    decimal InvoicedQuantity,
    decimal? PurchaseOrderUnitPrice,
    decimal InvoiceUnitPrice,
    decimal LineAmount);

public sealed record PaymentBatchSummary(
    Guid Id,
    string BatchNumber,
    string BankReference,
    decimal TotalAmount,
    int PayableCount,
    DateTime PaidOnUtc);

public sealed record PayableDetailResponse(
    PayableResponse Payable,
    string? PurchaseOrderNumber,
    string? GoodsReceiptNumber,
    DateTime? AcceptedOnUtc,
    IReadOnlyCollection<PayableDetailLine> Lines,
    PaymentBatchSummary? PaymentBatch,
    IReadOnlyCollection<JournalEntryResponse> JournalEntries);

public sealed record PaymentBatchResponse(
    Guid Id,
    string BatchNumber,
    Guid SupplierId,
    string Currency,
    decimal TotalAmount,
    string BankReference,
    DateTime PaidOnUtc,
    IReadOnlyCollection<PayableResponse> Payables);
