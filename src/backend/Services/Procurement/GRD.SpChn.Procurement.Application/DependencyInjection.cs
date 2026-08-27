using FluentValidation;
using GRD.SpChn.Procurement.Application.Behaviors;
using GRD.SpChn.Procurement.Application.PurchaseOrders;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Procurement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<ProcurementProcessManager>();

        return services;
    }
}
