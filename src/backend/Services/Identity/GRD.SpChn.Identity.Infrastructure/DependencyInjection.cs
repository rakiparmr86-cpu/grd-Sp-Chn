using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Identity.Application.Abstractions;
using GRD.SpChn.Identity.Infrastructure.Persistence;
using GRD.SpChn.Identity.Infrastructure.Security;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddErpTokenIssuer(configuration);
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IAccessProfileRepository, AccessProfileRepository>();
        services.AddSingleton<IPasswordVerifier, Pbkdf2PasswordVerifier>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }
}
