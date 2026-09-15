using GRD.SpChn.Accounting.Application;
using GRD.SpChn.Accounting.Infrastructure;
using GRD.SpChn.Observability;
using GRD.SpChn.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
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

public partial class Program;
