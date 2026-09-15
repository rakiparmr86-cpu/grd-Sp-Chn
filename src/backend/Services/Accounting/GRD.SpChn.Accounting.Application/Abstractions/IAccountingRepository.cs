using GRD.SpChn.Accounting.Application.Payables;
using GRD.SpChn.Accounting.Domain;
using GRD.SpChn.Contracts.IntegrationEvents;

namespace GRD.SpChn.Accounting.Application.Abstractions;

public interface IAccountingRepository
{
    Task RecordPurchaseOrderAsync(
        PurchaseOrderIssuedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);

    Task RecordAcceptedReceiptAsync(
        QualityInspectionApprovedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);

    Task ReconcileGoodsReceiptAccrualsAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<InvoiceMatchContext?> GetInvoiceMatchContextAsync(
        Guid purchaseOrderId,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default);

    Task<bool> SupplierInvoiceExistsAsync(
        Guid supplierId,
        string supplierInvoiceNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, decimal>> GetInvoicedQuantitiesAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default);

    Task AddPayableAsync(VendorPayable payable, CancellationToken cancellationToken = default);
    Task<VendorPayable?> GetPayableForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdatePayableAsync(VendorPayable payable, CancellationToken cancellationToken = default);
    Task AddPaymentAsync(Guid paymentId, VendorPayable payable, CancellationToken cancellationToken = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PayableResponse>> ListPayablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<JournalEntryResponse>> ListJournalEntriesAsync(CancellationToken cancellationToken = default);
}
