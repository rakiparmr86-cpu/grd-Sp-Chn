using GRD.SpChn.Inventory.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Inventory.Application.Behaviors;

internal sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        request is ITransactionalRequest
            ? unitOfWork.ExecuteAsync(_ => next(), cancellationToken)
            : next();
}
