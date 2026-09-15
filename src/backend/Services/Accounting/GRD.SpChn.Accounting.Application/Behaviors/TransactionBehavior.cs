using GRD.SpChn.Accounting.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Accounting.Application.Behaviors;

internal sealed class TransactionBehavior<TRequest, TResponse>(IAccountingUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        request is IAccountingTransactionalRequest
            ? unitOfWork.ExecuteAsync(_ => next(), cancellationToken)
            : next();
}
