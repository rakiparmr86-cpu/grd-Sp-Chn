using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Observability;
using GRD.SpChn.OutboxPublisher;

var builder = Host.CreateApplicationBuilder(args);

builder.AddObservability();
builder.Services.AddRabbitMqEventBus(builder.Configuration);
builder.Services
    .AddOptions<OutboxPublisherOptions>()
    .Bind(builder.Configuration.GetSection(OutboxPublisherOptions.SectionName));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
