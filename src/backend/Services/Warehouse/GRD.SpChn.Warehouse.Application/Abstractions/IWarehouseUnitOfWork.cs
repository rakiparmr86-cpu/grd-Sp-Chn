namespace GRD.SpChn.Warehouse.Application.Abstractions;

public interface IWarehouseUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public interface IWarehouseTransactionalRequest;
