using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Payables;

public sealed record ApproveVendorPayableCommand(Guid PayableId, Guid ApprovedByUserId)
    : IRequest<Result<PayableResponse>>, IAccountingTransactionalRequest;

internal sealed class ApproveVendorPayableCommandHandler(
    IAccountingRepository repository,
    IAccountingOutboxWriter outbox)
    : IRequestHandler<ApproveVendorPayableCommand, Result<PayableResponse>>
{
    public async Task<Result<PayableResponse>> Handle(
        ApproveVendorPayableCommand request,
        CancellationToken cancellationToken)
    {
        var payable = await repository.GetPayableForUpdateAsync(request.PayableId, cancellationToken);
        if (payable is null)
            return Result<PayableResponse>.Failure(Error.NotFound(
                "Accounting.PayableNotFound", $"Payable '{request.PayableId}' was not found."));

        try
        {
            payable.Approve(request.ApprovedByUserId);
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
            return Result<PayableResponse>.Success(CreateVendorInvoiceCommandHandler.Map(payable));
        }
        catch (InvalidOperationException exception)
        {
            return Result<PayableResponse>.Failure(Error.Conflict("Accounting.ApprovalRejected", exception.Message));
        }
    }
}
