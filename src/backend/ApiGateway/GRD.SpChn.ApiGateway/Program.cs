using GRD.SpChn.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseServiceDefaults();

app.MapGet(
    "/",
    () => Results.Ok(new
    {
        service = app.Environment.ApplicationName,
        environment = app.Environment.EnvironmentName,
        status = "running"
    }));

app.MapServiceDefaultEndpoints();
app.MapReverseProxy();

app.Run();

// Enables WebApplicationFactory<Program> in integration tests.
public partial class Program;
