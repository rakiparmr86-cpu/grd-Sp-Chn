using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Domain;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Payables;

// Records one executed bank transfer that settles one or more approved payables of
// a single supplier. A single payment is a batch of one.
public sealed record RecordVendorPaymentBatchCommand(
    IReadOnlyCollection<Guid> PayableIds,
    Guid PaidByUserId,
    string BankReference,
    DateTime PaidOnUtc)
    : IRequest<Result<PaymentBatchResponse>>, IAccountingTransactionalRequest;

internal sealed class RecordVendorPaymentBatchCommandHandler(
    IAccountingRepository repository,
    IAccountingOutboxWriter outbox)
    : IRequestHandler<RecordVendorPaymentBatchCommand, Result<PaymentBatchResponse>>
{
    public async Task<Result<PaymentBatchResponse>> Handle(
        RecordVendorPaymentBatchCommand request,
        CancellationToken cancellationToken)
    {
        var ids = request.PayableIds.Distinct().Order().ToArray();
        if (ids.Length == 0 || ids.Length > ApproveVendorPayablesCommandHandler.MaxBatchSize)
            return Validation("Accounting.InvalidPaymentBatch",
                $"Select between 1 and {ApproveVendorPayablesCommandHandler.MaxBatchSize} approved payables to pay.");

        var bankReference = request.BankReference?.Trim() ?? string.Empty;
        if (bankReference.Length == 0)
            return Validation("Accounting.InvalidPayment", "The bank transaction reference is required.");
        if (await repository.BankReferenceExistsAsync(bankReference, cancellationToken))
            return Result<PaymentBatchResponse>.Failure(Error.Conflict(
                "Accounting.DuplicateBankReference",
                $"Bank reference '{bankReference}' has already been used for another payment."));

        // The transaction commits even when a failure Result is returned, so every
        // payable is validated in memory before anything is written.
        var payables = new List<VendorPayable>(ids.Length);
        foreach (var id in ids)
        {
            var payable = await repository.GetPayableForUpdateAsync(id, cancellationToken);
            if (payable is null)
                return Result<PaymentBatchResponse>.Failure(Error.NotFound(
                    "Accounting.PayableNotFound", $"Payable '{id}' was not found."));

            if (payables.Count > 0 &&
                (payable.SupplierId != payables[0].SupplierId || payable.Currency != payables[0].Currency))
                return Validation("Accounting.MixedPaymentBatch",
                    "A payment batch can only contain payables of one supplier in one currency.");

            try
            {
                payable.RecordPayment(request.PaidByUserId, bankReference, request.PaidOnUtc);
            }
            catch (ArgumentException exception)
            {
                return Validation("Accounting.InvalidPayment", exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Result<PaymentBatchResponse>.Failure(Error.Conflict(
                    "Accounting.PaymentRejected",
                    $"{payable.SupplierInvoiceNumber} ({payable.InvoiceNumber}): {exception.Message}"));
            }

            payables.Add(payable);
        }

        var first = payables[0];
        var batchId = Guid.NewGuid();
        var batchNumber = $"PB-{DateTime.UtcNow:yyyyMMddHHmmss}-{batchId:N}"[..30];
        var total = payables.Sum(payable => payable.TotalAmount);

        await repository.AddPaymentBatchAsync(
            batchId, batchNumber, first.SupplierId, first.Currency, total, payables.Count,
            bankReference, request.PaidByUserId, request.PaidOnUtc, cancellationToken);

        foreach (var payable in payables)
        {
            var paymentId = Guid.NewGuid();
            await repository.UpdatePayableAsync(payable, cancellationToken);
            await repository.AddPaymentAsync(paymentId, payable, batchId, cancellationToken);
            await outbox.AddAsync(
                new VendorPaymentReleasedIntegrationEvent(
                    paymentId,
                    payable.Id,
                    payable.PurchaseOrderId,
                    payable.GoodsReceiptId,
                    payable.SupplierId,
                    payable.TotalAmount,
                    payable.Currency,
                    bankReference,
                    request.PaidOnUtc)
                {
                    OccurredOnUtc = request.PaidOnUtc
                },
                MessagingTopology.AccountingExchange,
                MessagingTopology.VendorPaymentReleasedRoutingKey,
                cancellationToken);
        }

        // One bank transfer = one ledger entry for the batch total.
        await repository.AddJournalEntryAsync(JournalEntry.Create(
            "VendorPayment",
            "AccountingPaymentBatch",
            batchId,
            first.Currency,
            payables.Count == 1
                ? $"Vendor payment {bankReference} for {first.InvoiceNumber}"
                : $"Vendor payment {bankReference} for {payables.Count} invoices ({batchNumber})",
            [
                JournalLine.DebitLine("VENDOR_PAYABLE", total),
                JournalLine.CreditLine("BANK", total)
            ],
            request.PaidOnUtc), cancellationToken);

        await outbox.AddAsync(
            new ActivityNotificationRequestedIntegrationEvent(
                "accounting.vendor-payment.released",
                "VendorPaymentBatch",
                batchId,
                payables.Count == 1
                    ? $"Vendor payment recorded for {first.InvoiceNumber}"
                    : $"Vendor payment batch {batchNumber} recorded",
                $"Payment {bankReference} for {total:0.00} {first.Currency} settled {payables.Count} invoice(s).",
                [],
                ["accounting.payable.read"])
            {
                OccurredOnUtc = request.PaidOnUtc
            },
            MessagingTopology.NotificationExchange,
            MessagingTopology.NotificationRequestedRoutingKey,
            cancellationToken);

        return Result<PaymentBatchResponse>.Success(new PaymentBatchResponse(
            batchId,
            batchNumber,
            first.SupplierId,
            first.Currency,
            total,
            bankReference,
            request.PaidOnUtc,
            payables.Select(CreateVendorInvoiceCommandHandler.Map).ToArray()));
    }

    private static Result<PaymentBatchResponse> Validation(string code, string description) =>
        Result<PaymentBatchResponse>.Failure(Error.Validation(code, description));
}
