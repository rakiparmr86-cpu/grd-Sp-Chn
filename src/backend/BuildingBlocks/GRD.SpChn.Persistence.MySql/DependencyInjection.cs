using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Persistence.MySql;

public static class DependencyInjection
{
    public const string DefaultConnectionStringName = "Database";

    public static IServiceCollection AddMySqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = DefaultConnectionStringName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(connectionStringName);

        services.AddSingleton<IDbConnectionFactory>(
            new MySqlConnectionFactory(connectionString));

        return services;
    }
}
