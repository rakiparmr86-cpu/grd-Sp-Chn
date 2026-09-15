namespace GRD.SpChn.Accounting.Application.Abstractions;

public interface IAccountingUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public interface IAccountingTransactionalRequest;
