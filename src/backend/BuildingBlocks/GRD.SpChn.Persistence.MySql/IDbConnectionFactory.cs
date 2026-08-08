using System.Data.Common;

namespace GRD.SpChn.Persistence.MySql;

/// <summary>
/// Opens database connections for Dapper-based repositories.
/// The caller owns and must dispose the returned connection.
/// </summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default);
}
