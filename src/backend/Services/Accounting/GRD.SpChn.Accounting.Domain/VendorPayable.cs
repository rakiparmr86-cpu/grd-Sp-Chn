namespace GRD.SpChn.Accounting.Domain;

public enum VendorPayableStatus
{
    PendingApproval,
    Approved,
    Paid
}

public sealed record VendorInvoiceLine(
    Guid ProductId,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice)
{
    public decimal LineAmount => decimal.Round(Quantity * UnitPrice, 2);
}

public sealed class VendorPayable
{
    private readonly IReadOnlyCollection<VendorInvoiceLine> _lines;

    private VendorPayable(
        Guid id,
        string invoiceNumber,
        string supplierInvoiceNumber,
        Guid purchaseOrderId,
        Guid goodsReceiptId,
        Guid supplierId,
        string currency,
        decimal taxAmount,
        VendorPayableStatus status,
        Guid createdByUserId,
        Guid? approvedByUserId,
        Guid? paidByUserId,
        string? bankReference,
        DateTime invoiceDateUtc,
        DateTime dueDateUtc,
        DateTime createdOnUtc,
        DateTime? approvedOnUtc,
        DateTime? paidOnUtc,
        IReadOnlyCollection<VendorInvoiceLine> lines)
    {
        Id = id;
        InvoiceNumber = invoiceNumber;
        SupplierInvoiceNumber = supplierInvoiceNumber;
        PurchaseOrderId = purchaseOrderId;
        GoodsReceiptId = goodsReceiptId;
        SupplierId = supplierId;
        Currency = currency;
        TaxAmount = taxAmount;
        Status = status;
        CreatedByUserId = createdByUserId;
        ApprovedByUserId = approvedByUserId;
        PaidByUserId = paidByUserId;
        BankReference = bankReference;
        InvoiceDateUtc = invoiceDateUtc;
        DueDateUtc = dueDateUtc;
        CreatedOnUtc = createdOnUtc;
        ApprovedOnUtc = approvedOnUtc;
        PaidOnUtc = paidOnUtc;
        _lines = lines;
    }

    public Guid Id { get; }
    public string InvoiceNumber { get; }
    public string SupplierInvoiceNumber { get; }
    public Guid PurchaseOrderId { get; }
    public Guid GoodsReceiptId { get; }
    public Guid SupplierId { get; }
    public string Currency { get; }
    public decimal TaxAmount { get; }
    public VendorPayableStatus Status { get; private set; }
    public Guid CreatedByUserId { get; }
    public Guid? ApprovedByUserId { get; private set; }
    public Guid? PaidByUserId { get; private set; }
    public string? BankReference { get; private set; }
    public DateTime InvoiceDateUtc { get; }
    public DateTime DueDateUtc { get; }
    public DateTime CreatedOnUtc { get; }
    public DateTime? ApprovedOnUtc { get; private set; }
    public DateTime? PaidOnUtc { get; private set; }
    public IReadOnlyCollection<VendorInvoiceLine> Lines => _lines;
    public decimal Subtotal => _lines.Sum(line => line.LineAmount);
    public decimal TotalAmount => Subtotal + TaxAmount;

    public static VendorPayable Create(
        string supplierInvoiceNumber,
        Guid purchaseOrderId,
        Guid goodsReceiptId,
        Guid supplierId,
        string currency,
        decimal taxAmount,
        Guid createdByUserId,
        DateTime invoiceDateUtc,
        DateTime dueDateUtc,
        IReadOnlyCollection<VendorInvoiceLine> lines,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(supplierInvoiceNumber))
            throw new ArgumentException("The supplier invoice number is required.", nameof(supplierInvoiceNumber));
        if (purchaseOrderId == Guid.Empty || goodsReceiptId == Guid.Empty || supplierId == Guid.Empty)
            throw new ArgumentException("Purchase order, goods receipt, and supplier are required.");
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("The employee recording the invoice is required.", nameof(createdByUserId));
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new ArgumentException("A three-letter currency code is required.", nameof(currency));
        if (taxAmount < 0)
            throw new ArgumentException("Tax cannot be negative.", nameof(taxAmount));
        if (lines.Count == 0 || lines.Any(line =>
                line.ProductId == Guid.Empty || line.Quantity <= 0 || line.UnitPrice <= 0 ||
                string.IsNullOrWhiteSpace(line.UnitOfMeasure)))
            throw new ArgumentException("At least one valid positive invoice line is required.", nameof(lines));
        if (dueDateUtc.Date < invoiceDateUtc.Date)
            throw new ArgumentException("The due date cannot be before the invoice date.", nameof(dueDateUtc));

        var id = Guid.NewGuid();
        var now = utcNow ?? DateTime.UtcNow;
        return new VendorPayable(
            id,
            $"VI-{now:yyyyMMddHHmmss}-{id:N}"[..30],
            supplierInvoiceNumber.Trim(),
            purchaseOrderId,
            goodsReceiptId,
            supplierId,
            currency.Trim().ToUpperInvariant(),
            decimal.Round(taxAmount, 2),
            VendorPayableStatus.PendingApproval,
            createdByUserId,
            null,
            null,
            null,
            invoiceDateUtc,
            dueDateUtc,
            now,
            null,
            null,
            lines);
    }

    public static VendorPayable Rehydrate(
        Guid id,
        string invoiceNumber,
        string supplierInvoiceNumber,
        Guid purchaseOrderId,
        Guid goodsReceiptId,
        Guid supplierId,
        string currency,
        decimal taxAmount,
        VendorPayableStatus status,
        Guid createdByUserId,
        Guid? approvedByUserId,
        Guid? paidByUserId,
        string? bankReference,
        DateTime invoiceDateUtc,
        DateTime dueDateUtc,
        DateTime createdOnUtc,
        DateTime? approvedOnUtc,
        DateTime? paidOnUtc,
        IReadOnlyCollection<VendorInvoiceLine> lines) =>
        new(id, invoiceNumber, supplierInvoiceNumber, purchaseOrderId, goodsReceiptId,
            supplierId, currency, taxAmount, status, createdByUserId, approvedByUserId,
            paidByUserId, bankReference, invoiceDateUtc, dueDateUtc, createdOnUtc,
            approvedOnUtc, paidOnUtc, lines);

    public void Approve(Guid approvedByUserId, DateTime? utcNow = null)
    {
        if (Status != VendorPayableStatus.PendingApproval)
            throw new InvalidOperationException($"Payable {Id} cannot be approved from {Status}.");
        if (approvedByUserId == Guid.Empty)
            throw new ArgumentException("An approver is required.", nameof(approvedByUserId));
        if (approvedByUserId == CreatedByUserId)
            throw new InvalidOperationException("The invoice creator cannot approve the same payable.");

        Status = VendorPayableStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedOnUtc = utcNow ?? DateTime.UtcNow;
    }

    public void RecordPayment(
        Guid paidByUserId,
        string bankReference,
        DateTime paidOnUtc)
    {
        if (Status != VendorPayableStatus.Approved)
            throw new InvalidOperationException($"Payable {Id} cannot be paid from {Status}.");
        if (paidByUserId == Guid.Empty)
            throw new ArgumentException("The employee recording payment is required.", nameof(paidByUserId));
        if (string.IsNullOrWhiteSpace(bankReference))
            throw new ArgumentException("The bank transaction reference is required.", nameof(bankReference));
        if (paidOnUtc > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("Payment date cannot be in the future.", nameof(paidOnUtc));

        Status = VendorPayableStatus.Paid;
        PaidByUserId = paidByUserId;
        BankReference = bankReference.Trim();
        PaidOnUtc = paidOnUtc;
    }
}
