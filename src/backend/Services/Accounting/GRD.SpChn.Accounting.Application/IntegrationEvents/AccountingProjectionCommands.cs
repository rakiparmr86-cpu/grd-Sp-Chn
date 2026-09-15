using GRD.SpChn.Accounting.Application.Abstractions;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.SharedKernel;
using MediatR;

namespace GRD.SpChn.Accounting.Application.IntegrationEvents;

public sealed record RecordPurchaseOrderForAccountingCommand(
    PurchaseOrderIssuedIntegrationEvent IntegrationEvent)
    : IRequest<Result<bool>>, IAccountingTransactionalRequest;

public sealed record RecordAcceptedReceiptForAccountingCommand(
    QualityInspectionApprovedIntegrationEvent IntegrationEvent)
    : IRequest<Result<bool>>, IAccountingTransactionalRequest;

internal sealed class RecordPurchaseOrderForAccountingCommandHandler(
    IAccountingInboxStore inbox,
    IAccountingRepository repository)
    : IRequestHandler<RecordPurchaseOrderForAccountingCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RecordPurchaseOrderForAccountingCommand request,
        CancellationToken cancellationToken)
    {
        var message = request.IntegrationEvent;
        if (!await inbox.TryAddAsync(message.EventId, nameof(PurchaseOrderIssuedIntegrationEvent), cancellationToken))
            return Result<bool>.Success(false);

        await repository.RecordPurchaseOrderAsync(message, cancellationToken);
        await repository.ReconcileGoodsReceiptAccrualsAsync(message.PurchaseOrderId, cancellationToken);
        return Result<bool>.Success(true);
    }
}

internal sealed class RecordAcceptedReceiptForAccountingCommandHandler(
    IAccountingInboxStore inbox,
    IAccountingRepository repository)
    : IRequestHandler<RecordAcceptedReceiptForAccountingCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RecordAcceptedReceiptForAccountingCommand request,
        CancellationToken cancellationToken)
    {
        var message = request.IntegrationEvent;
        if (!await inbox.TryAddAsync(message.EventId, nameof(QualityInspectionApprovedIntegrationEvent), cancellationToken))
            return Result<bool>.Success(false);

        await repository.RecordAcceptedReceiptAsync(message, cancellationToken);
        await repository.ReconcileGoodsReceiptAccrualsAsync(message.PurchaseOrderId, cancellationToken);
        return Result<bool>.Success(true);
    }
}
