using FluentValidation;
using GRD.SpChn.OrderManagement.Application.Behaviors;
using GRD.SpChn.OrderManagement.Application.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.OrderManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<OrderProcessManager>();

        return services;
    }
}
