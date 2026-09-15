using GRD.SpChn.Observability;
using GRD.SpChn.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
GRD.SpChn.Inventory.Application.DependencyInjection.AddApplication(builder.Services);
GRD.SpChn.Warehouse.Application.DependencyInjection.AddApplication(builder.Services);
GRD.SpChn.Inventory.Infrastructure.DependencyInjection.AddInfrastructure(
    builder.Services,
    builder.Configuration);
GRD.SpChn.Warehouse.Infrastructure.DependencyInjection.AddInfrastructure(
    builder.Services,
    builder.Configuration,
    registerSharedInfrastructure: false);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddErpAuthentication(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDarkSwaggerUi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapServiceDefaultEndpoints();

app.Run();

// Enables WebApplicationFactory<Program> in integration tests.
public partial class Program;
