using GRD.SpChn.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseDarkSwaggerUi(
        [
            new SwaggerDocumentEndpoint("Delivery v1", "/api/delivery/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Identity v1", "/api/identity/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Inventory v1", "/api/inventory/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Notifications v1", "/api/notifications/openapi/v1.json"),
            new SwaggerDocumentEndpoint(
                "Order Management v1",
                "/swagger-docs/order-management/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Organization v1", "/api/organization/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Procurement v1", "/api/procurement/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Product Catalog v1", "/api/products/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Reporting v1", "/api/reports/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Shipment v1", "/api/shipments/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Supplier v1", "/api/suppliers/openapi/v1.json"),
            new SwaggerDocumentEndpoint(
                "Transportation v1",
                "/api/transportation/openapi/v1.json"),
            new SwaggerDocumentEndpoint("Warehouse v1", "/api/warehouses/openapi/v1.json")
        ],
        [
            new SwaggerRequestRoute("http://localhost:5294", "/api/delivery"),
            new SwaggerRequestRoute("http://localhost:7001", "/api/identity"),
            new SwaggerRequestRoute("http://localhost:5018", "/api/inventory"),
            new SwaggerRequestRoute("http://localhost:7002", "/api/notifications"),
            new SwaggerRequestRoute("http://localhost:5255", string.Empty),
            new SwaggerRequestRoute("http://localhost:5218", "/api/organization"),
            new SwaggerRequestRoute("http://localhost:5112", "/api/procurement"),
            new SwaggerRequestRoute("http://localhost:5006", "/api/products"),
            new SwaggerRequestRoute("http://localhost:5274", "/api/reports"),
            new SwaggerRequestRoute("http://localhost:5059", "/api/shipments"),
            new SwaggerRequestRoute("http://localhost:5141", "/api/suppliers"),
            new SwaggerRequestRoute("http://localhost:5258", "/api/transportation"),
            new SwaggerRequestRoute("http://localhost:5276", "/api/warehouses")
        ]);
}

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
