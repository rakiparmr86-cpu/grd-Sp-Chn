using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Domain;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Payables;

public sealed record RecordVendorPaymentCommand(
    Guid PayableId,
    Guid PaidByUserId,
    string BankReference,
    DateTime PaidOnUtc)
    : IRequest<Result<PayableResponse>>, IAccountingTransactionalRequest;

internal sealed class RecordVendorPaymentCommandHandler(
    IAccountingRepository repository,
    IAccountingOutboxWriter outbox)
    : IRequestHandler<RecordVendorPaymentCommand, Result<PayableResponse>>
{
    public async Task<Result<PayableResponse>> Handle(
        RecordVendorPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var payable = await repository.GetPayableForUpdateAsync(request.PayableId, cancellationToken);
        if (payable is null)
            return Result<PayableResponse>.Failure(Error.NotFound(
                "Accounting.PayableNotFound", $"Payable '{request.PayableId}' was not found."));

        try
        {
            payable.RecordPayment(request.PaidByUserId, request.BankReference, request.PaidOnUtc);
            var paymentId = Guid.NewGuid();
            await repository.UpdatePayableAsync(payable, cancellationToken);
            await repository.AddPaymentAsync(paymentId, payable, cancellationToken);
            await repository.AddJournalEntryAsync(JournalEntry.Create(
                "VendorPayment",
                "AccountingPayment",
                paymentId,
                payable.Currency,
                $"Vendor payment {payable.BankReference} for {payable.InvoiceNumber}",
                [
                    JournalLine.DebitLine("VENDOR_PAYABLE", payable.TotalAmount),
                    JournalLine.CreditLine("BANK", payable.TotalAmount)
                ],
                payable.PaidOnUtc), cancellationToken);

            await outbox.AddAsync(
                new VendorPaymentReleasedIntegrationEvent(
                    paymentId,
                    payable.Id,
                    payable.PurchaseOrderId,
                    payable.GoodsReceiptId,
                    payable.SupplierId,
                    payable.TotalAmount,
                    payable.Currency,
                    payable.BankReference!,
                    payable.PaidOnUtc!.Value)
                {
                    OccurredOnUtc = payable.PaidOnUtc.Value
                },
                MessagingTopology.AccountingExchange,
                MessagingTopology.VendorPaymentReleasedRoutingKey,
                cancellationToken);
            await outbox.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    "accounting.vendor-payment.released",
                    "VendorPayable",
                    payable.Id,
                    $"Vendor payment recorded for {payable.InvoiceNumber}",
                    $"Payment {payable.BankReference} for {payable.TotalAmount:0.00} {payable.Currency} was recorded.",
                    [],
                    ["accounting.payable.read"])
                {
                    OccurredOnUtc = payable.PaidOnUtc.Value
                },
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);

            return Result<PayableResponse>.Success(CreateVendorInvoiceCommandHandler.Map(payable));
        }
        catch (ArgumentException exception)
        {
            return Result<PayableResponse>.Failure(Error.Validation("Accounting.InvalidPayment", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Result<PayableResponse>.Failure(Error.Conflict("Accounting.PaymentRejected", exception.Message));
        }
    }
}
