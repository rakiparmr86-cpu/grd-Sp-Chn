using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.ProductCatalog.Application.Abstractions;
using GRD.SpChn.ProductCatalog.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.ProductCatalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<ICatalogRepository, CatalogRepository>();

        return services;
    }
}
