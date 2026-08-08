using GRD.SpChn.EventBus.RabbitMQ;
using GRD.SpChn.Observability;
using GRD.SpChn.OutboxPublisher;
using GRD.SpChn.Persistence.MySql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddObservability();
builder.Services.AddMySqlPersistence(builder.Configuration);
builder.Services.AddRabbitMqEventBus(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
