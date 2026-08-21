using System.Data.Common;
using GRD.SpChn.Inventory.Application.Abstractions;
using GRD.SpChn.Persistence.MySql;

namespace GRD.SpChn.Inventory.Infrastructure.Persistence;

internal sealed class InventoryUnitOfWork(IDbConnectionFactory connectionFactory) : IUnitOfWork
{
    private DbConnection? _connection;
    private DbTransaction? _transaction;

    internal DbConnection Connection => _connection
        ?? throw new InvalidOperationException("No Inventory transaction is active.");

    internal DbTransaction Transaction => _transaction
        ?? throw new InvalidOperationException("No Inventory transaction is active.");

    internal bool HasActiveTransaction => _transaction is not null;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (HasActiveTransaction)
        {
            return await operation(cancellationToken);
        }

        _connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
            _transaction = null;
            _connection = null;
        }
    }
}
