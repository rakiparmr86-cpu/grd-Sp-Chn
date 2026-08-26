using GRD.SpChn.Supplier.Application;
using GRD.SpChn.Supplier.Infrastructure;
using GRD.SpChn.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDarkSwaggerUi();
}

app.UseAuthorization();
app.MapControllers();
app.MapServiceDefaultEndpoints();

app.Run();

// Enables WebApplicationFactory<Program> in integration tests.
public partial class Program;
