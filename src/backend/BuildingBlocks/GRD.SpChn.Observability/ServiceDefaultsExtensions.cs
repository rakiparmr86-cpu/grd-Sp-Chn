using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GRD.SpChn.Observability;

/// <summary>
/// Defines the common entry-point behavior used by every HTTP service.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string LiveTag = "live";
    private const string SolutionFileName = "GRD.SpChn.sln";
    private const string DefaultErrorLogFileName = "grd-errors-.log";
    private const long DefaultErrorLogSizeLimitBytes = 20 * 1024 * 1024;
    private const int DefaultRetainedErrorLogFileCount = 14;

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
                .Enrich.With<ActivityTraceEnricher>()
                .Enrich.WithProperty("Service", builder.Environment.ApplicationName)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .WriteTo.Console();

            if (IsSharedErrorLogEnabled(builder.Configuration, builder.Environment))
            {
                var errorLogPath = ResolveSharedErrorLogPath(
                    builder.Configuration,
                    builder.Environment);
                var errorLogDirectory = Path.GetDirectoryName(errorLogPath)
                    ?? throw new InvalidOperationException(
                        $"The shared error log path has no directory: {errorLogPath}");

                Directory.CreateDirectory(errorLogDirectory);

                loggerConfiguration.WriteTo.File(
                    errorLogPath,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                        "[{Level:u3}] [{Service}] [TraceId:{TraceId}] " +
                        "[SpanId:{SpanId}] [{SourceContext}] " +
                        "{Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: DefaultErrorLogSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: DefaultRetainedErrorLogFileCount,
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1));
            }
        });

        return builder;
    }

    private static bool IsSharedErrorLogEnabled(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return configuration.GetValue<bool?>("Observability:FileLogging:Enabled")
            ?? environment.IsDevelopment();
    }

    private static string ResolveSharedErrorLogPath(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration["Observability:FileLogging:ErrorLogPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath, environment.ContentRootPath);
        }

        var repositoryRoot = FindRepositoryRoot(environment.ContentRootPath)
            ?? FindRepositoryRoot(Directory.GetCurrentDirectory());
        var logRoot = repositoryRoot ?? environment.ContentRootPath;

        return Path.Combine(logRoot, "logs", DefaultErrorLogFileName);
    }

    private static string? FindRepositoryRoot(string startingPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startingPath));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
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

    private sealed class ActivityTraceEnricher : ILogEventEnricher
    {
        public void Enrich(
            LogEvent logEvent,
            ILogEventPropertyFactory propertyFactory)
        {
            var activity = Activity.Current;
            if (activity is null)
            {
                return;
            }

            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        }
    }
}
