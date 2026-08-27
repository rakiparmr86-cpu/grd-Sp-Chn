using GRD.SpChn.Warehouse.Application.Abstractions;
using MediatR;

namespace GRD.SpChn.Warehouse.Application.Behaviors;

internal sealed class TransactionBehavior<TRequest, TResponse>(IWarehouseUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        request is IWarehouseTransactionalRequest
            ? unitOfWork.ExecuteAsync(_ => next(), cancellationToken)
            : next();
}
