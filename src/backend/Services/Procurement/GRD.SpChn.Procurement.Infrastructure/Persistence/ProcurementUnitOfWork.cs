using System.Data.Common;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Procurement.Application.Abstractions;

namespace GRD.SpChn.Procurement.Infrastructure.Persistence;

internal sealed class ProcurementUnitOfWork(IDbConnectionFactory connectionFactory) : IUnitOfWork
{
    private DbConnection? _connection;
    private DbTransaction? _transaction;

    internal DbConnection Connection => _connection
        ?? throw new InvalidOperationException("No Procurement transaction is active.");
    internal DbTransaction Transaction => _transaction
        ?? throw new InvalidOperationException("No Procurement transaction is active.");

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (_transaction is not null) return await operation(cancellationToken);
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
