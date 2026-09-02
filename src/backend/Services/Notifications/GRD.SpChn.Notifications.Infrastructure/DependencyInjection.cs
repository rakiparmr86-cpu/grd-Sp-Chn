using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Persistence.MySql;
using GRD.SpChn.Contracts.IntegrationEvents;
using GRD.SpChn.Notifications.Application.Abstractions;
using GRD.SpChn.Notifications.Application.IntegrationEvents;
using GRD.SpChn.Notifications.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GRD.SpChn.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMySqlPersistence(configuration);
        services.AddRabbitMqEventBus(configuration);
        services.AddScoped<IEmailNotificationQueue, EmailNotificationQueue>();
        services
            .AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName));
        services.AddHostedService<EmailDeliveryWorker>();
        services.AddRabbitMqConsumer<
            ActivityNotificationRequestedIntegrationEvent,
            ActivityNotificationRequestedIntegrationEventHandler>(
            MessagingTopology.NotificationExchange,
            "notifications.activity-email",
            MessagingTopology.NotificationRequestedRoutingKey);

        return services;
    }
}
