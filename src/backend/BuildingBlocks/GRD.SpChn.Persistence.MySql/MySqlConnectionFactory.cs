using System.Data.Common;
using MySqlConnector;

namespace GRD.SpChn.Persistence.MySql;

internal sealed class MySqlConnectionFactory(string? connectionString)
    : IDbConnectionFactory
{
    public async ValueTask<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The 'Database' connection string is not configured. " +
                "Set ConnectionStrings__Database for this service.");
        }

        var connection = new MySqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
