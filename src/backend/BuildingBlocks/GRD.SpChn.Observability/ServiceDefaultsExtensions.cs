using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GRD.SpChn.Observability;

/// <summary>
/// Defines the common entry-point behavior used by every HTTP service.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string LiveTag = "live";

    /// <summary>
    /// Registers structured logging for HTTP services and background workers.
    /// </summary>
    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", builder.Environment.ApplicationName)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .WriteTo.Console();
        });

        return builder;
    }

    /// <summary>
    /// Registers cross-cutting services before the application is built.
    /// Service-specific application and infrastructure registrations belong in
    /// the calling service's <c>Program.cs</c> before <c>builder.Build()</c>.
    /// </summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.AddObservability();
        builder.Services.AddProblemDetails();

        builder.Services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: [LiveTag]);

        return builder;
    }

    /// <summary>
    /// Adds middleware that should run for every HTTP request.
    /// </summary>
    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();

        return app;
    }

    /// <summary>
    /// Exposes Kubernetes/container-friendly liveness and readiness probes.
    /// Dependency-specific health checks should omit the <c>live</c> tag so they
    /// participate in readiness without taking down the liveness probe.
    /// </summary>
    public static IEndpointRouteBuilder MapServiceDefaultEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(LiveTag),
                AllowCachingResponses = false
            });

        endpoints.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = _ => true,
                AllowCachingResponses = false
            });

        return endpoints;
    }
}
