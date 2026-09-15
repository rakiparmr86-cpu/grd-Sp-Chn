using GRD.SpChn.Accounting.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Accounting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
        return services;
    }
}
