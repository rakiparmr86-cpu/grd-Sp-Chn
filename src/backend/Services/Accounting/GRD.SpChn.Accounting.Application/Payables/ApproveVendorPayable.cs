using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Accounting.Domain;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Payables;

// Approves one or more payables all-or-nothing. A single approval is a batch of one.
public sealed record ApproveVendorPayablesCommand(IReadOnlyCollection<Guid> PayableIds, Guid ApprovedByUserId)
    : IRequest<Result<IReadOnlyCollection<PayableResponse>>>, IAccountingTransactionalRequest;

internal sealed class ApproveVendorPayablesCommandHandler(
    IAccountingRepository repository,
    IAccountingOutboxWriter outbox)
    : IRequestHandler<ApproveVendorPayablesCommand, Result<IReadOnlyCollection<PayableResponse>>>
{
    internal const int MaxBatchSize = 100;

    public async Task<Result<IReadOnlyCollection<PayableResponse>>> Handle(
        ApproveVendorPayablesCommand request,
        CancellationToken cancellationToken)
    {
        var ids = request.PayableIds.Distinct().Order().ToArray();
        if (ids.Length == 0 || ids.Length > MaxBatchSize)
            return Failure(Error.Validation(
                "Accounting.InvalidApprovalBatch", $"Select between 1 and {MaxBatchSize} payables to approve."));

        // The transaction commits even when a failure Result is returned, so every
        // payable is validated in memory before anything is written.
        var payables = new List<VendorPayable>(ids.Length);
        foreach (var id in ids)
        {
            var payable = await repository.GetPayableForUpdateAsync(id, cancellationToken);
            if (payable is null)
                return Failure(Error.NotFound("Accounting.PayableNotFound", $"Payable '{id}' was not found."));

            try
            {
                payable.Approve(request.ApprovedByUserId);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return Failure(Error.Conflict(
                    "Accounting.ApprovalRejected",
                    $"{payable.SupplierInvoiceNumber} ({payable.InvoiceNumber}): {exception.Message}"));
            }

            payables.Add(payable);
        }

        foreach (var payable in payables)
        {
            await repository.UpdatePayableAsync(payable, cancellationToken);
            await outbox.AddAsync(
                new ActivityNotificationRequestedIntegrationEvent(
                    "accounting.payable.approved",
                    "VendorPayable",
                    payable.Id,
                    $"Payable {payable.InvoiceNumber} approved",
                    $"Payment of {payable.TotalAmount:0.00} {payable.Currency} may now be released after bank execution.",
                    [],
                    ["accounting.payment.release"]),
                MessagingTopology.NotificationExchange,
                MessagingTopology.NotificationRequestedRoutingKey,
                cancellationToken);
        }

        return Result<IReadOnlyCollection<PayableResponse>>.Success(
            payables.Select(CreateVendorInvoiceCommandHandler.Map).ToArray());
    }

    private static Result<IReadOnlyCollection<PayableResponse>> Failure(Error error) =>
        Result<IReadOnlyCollection<PayableResponse>>.Failure(error);
}
