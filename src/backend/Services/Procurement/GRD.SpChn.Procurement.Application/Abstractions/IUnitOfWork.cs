namespace GRD.SpChn.Procurement.Application.Abstractions;

public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public interface ITransactionalRequest;
