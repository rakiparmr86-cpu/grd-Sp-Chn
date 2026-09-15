using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Domain;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Payables;

public sealed record CreateVendorInvoiceCommand(
    Guid PurchaseOrderId,
    Guid GoodsReceiptId,
    string SupplierInvoiceNumber,
    DateTime InvoiceDateUtc,
    DateTime DueDateUtc,
    decimal TaxAmount,
    Guid CreatedByUserId,
    IReadOnlyCollection<InvoiceLineInput> Lines)
    : IRequest<Result<PayableResponse>>, IAccountingTransactionalRequest;

internal sealed class CreateVendorInvoiceCommandHandler(
    IAccountingRepository repository,
    IAccountingOutboxWriter outbox)
    : IRequestHandler<CreateVendorInvoiceCommand, Result<PayableResponse>>
{
    public async Task<Result<PayableResponse>> Handle(
        CreateVendorInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var context = await repository.GetInvoiceMatchContextAsync(
            request.PurchaseOrderId,
            request.GoodsReceiptId,
            cancellationToken);
        if (context is null)
            return Result<PayableResponse>.Failure(Error.NotFound(
                "Accounting.MatchContextNotFound",
                "The quality-approved receipt and purchase order are not available in Accounting yet."));

        if (await repository.SupplierInvoiceExistsAsync(
                context.SupplierId,
                request.SupplierInvoiceNumber.Trim(),
                cancellationToken))
            return Result<PayableResponse>.Failure(Error.Conflict(
                "Accounting.DuplicateSupplierInvoice",
                "This supplier invoice number has already been recorded for the vendor."));

        var invoiced = await repository.GetInvoicedQuantitiesAsync(
            request.GoodsReceiptId,
            cancellationToken);
        var expected = context.Items.ToDictionary(item => item.ProductId);
        var lines = new List<VendorInvoiceLine>();

        foreach (var input in request.Lines)
        {
            if (!expected.TryGetValue(input.ProductId, out var matched))
                return Validation("Accounting.ProductNotAccepted", $"Product {input.ProductId} is not on the accepted receipt.");
            var alreadyInvoiced = invoiced.GetValueOrDefault(input.ProductId);
            if (input.Quantity <= 0 || input.Quantity + alreadyInvoiced > matched.AcceptedQuantity)
                return Validation(
                    "Accounting.InvoiceQuantityExceedsAccepted",
                    $"Product {input.ProductId} may be invoiced up to {matched.AcceptedQuantity - alreadyInvoiced} {matched.UnitOfMeasure}.");
            if (decimal.Round(input.UnitPrice, 4) != decimal.Round(matched.PurchaseOrderUnitPrice, 4))
                return Validation(
                    "Accounting.InvoicePriceMismatch",
                    $"Product {input.ProductId} must use PO price {matched.PurchaseOrderUnitPrice:0.0000} {context.Currency}.");

            lines.Add(new VendorInvoiceLine(
                input.ProductId,
                input.Quantity,
                matched.UnitOfMeasure,
                input.UnitPrice));
        }

        if (lines.Select(line => line.ProductId).Distinct().Count() != lines.Count)
            return Validation("Accounting.DuplicateInvoiceLine", "Each product can appear only once on an invoice.");

        try
        {
            var payable = VendorPayable.Create(
                request.SupplierInvoiceNumber,
                context.PurchaseOrderId,
                context.GoodsReceiptId,
                context.SupplierId,
                context.Currency,
                request.TaxAmount,
                request.CreatedByUserId,
                request.InvoiceDateUtc,
                request.DueDateUtc,
                lines);
            await repository.AddPayableAsync(payable, cancellationToken);

            var journalLines = new List<JournalLine>
            {
                JournalLine.DebitLine("GRNI", payable.Subtotal)
            };
            if (payable.TaxAmount > 0)
                journalLines.Add(JournalLine.DebitLine("INPUT_TAX", payable.TaxAmount));
            journalLines.Add(JournalLine.CreditLine("VENDOR_PAYABLE", payable.TotalAmount));
            await repository.AddJournalEntryAsync(JournalEntry.Create(
                "VendorInvoice",
                "VendorPayable",
                payable.Id,
                payable.Currency,
                $"Supplier invoice {payable.SupplierInvoiceNumber} matched to {context.PurchaseOrderNumber} and {context.GoodsReceiptNumber}",
                journalLines), cancellationToken);

            await outbox.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    "accounting.vendor-invoice.recorded",
                    "VendorPayable",
                    payable.Id,
                    $"Vendor invoice {payable.SupplierInvoiceNumber} awaits approval",
                    $"Invoice {payable.InvoiceNumber} for {payable.TotalAmount:0.00} {payable.Currency} passed three-way matching.",
                    [],
                    ["accounting.payable.approve"]),
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);

            return Result<PayableResponse>.Success(Map(payable));
        }
        catch (ArgumentException exception)
        {
            return Validation("Accounting.InvalidInvoice", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Result<PayableResponse>.Failure(Error.Conflict("Accounting.InvalidInvoiceState", exception.Message));
        }
    }

    private static Result<PayableResponse> Validation(string code, string description) =>
        Result<PayableResponse>.Failure(Error.Validation(code, description));

    internal static PayableResponse Map(VendorPayable payable) => new(
        payable.Id, payable.InvoiceNumber, payable.SupplierInvoiceNumber,
        payable.PurchaseOrderId, payable.GoodsReceiptId, payable.SupplierId,
        payable.Currency, payable.Subtotal, payable.TaxAmount, payable.TotalAmount,
        payable.Status.ToString(), payable.CreatedByUserId, payable.ApprovedByUserId,
        payable.PaidByUserId, payable.BankReference, payable.InvoiceDateUtc,
        payable.DueDateUtc, payable.CreatedOnUtc, payable.ApprovedOnUtc, payable.PaidOnUtc);
}
