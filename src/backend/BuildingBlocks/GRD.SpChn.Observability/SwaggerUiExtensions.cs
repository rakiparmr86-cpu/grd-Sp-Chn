using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace GRD.SpChn.Observability;

/// <summary>
/// Identifies an OpenAPI document displayed by the shared Swagger UI.
/// </summary>
/// <param name="Name">Human-readable name shown in the document selector.</param>
/// <param name="Url">Same-origin URL from which Swagger UI loads the document.</param>
public sealed record SwaggerDocumentEndpoint(string Name, string Url);

/// <summary>
/// Rewrites a downstream service origin to its public Gateway path for Swagger
/// "Try it out" requests.
/// </summary>
/// <param name="DestinationOrigin">Direct service origin present in its OpenAPI document.</param>
/// <param name="GatewayPathPrefix">Public Gateway prefix, or an empty string when paths are preserved.</param>
public sealed record SwaggerRequestRoute(
    string DestinationOrigin,
    string GatewayPathPrefix);

/// <summary>
/// Configures the development-only interactive API documentation experience.
/// </summary>
public static class SwaggerUiExtensions
{
    private const string ThemeRoute = "/swagger-ui/dark-theme.css";
    private const string ThemeResourceName =
        "GRD.SpChn.Observability.Swagger.dark-theme.css";

    private static readonly string DarkThemeCss = LoadDarkThemeCss();

    /// <summary>
    /// Exposes a dark Swagger UI for the current service's default OpenAPI document.
    /// Call this only when the OpenAPI endpoint is enabled, normally in Development.
    /// </summary>
    public static WebApplication UseDarkSwaggerUi(this WebApplication app)
    {
        var displayName = app.Environment.ApplicationName
            .Replace("GRD.SpChn.", string.Empty, StringComparison.Ordinal);

        return app.UseDarkSwaggerUi(
            [new SwaggerDocumentEndpoint($"{displayName} v1", "/openapi/v1.json")],
            []);
    }

    /// <summary>
    /// Exposes a dark Swagger UI containing one or more OpenAPI documents.
    /// This overload is used by the API Gateway to provide a service selector.
    /// </summary>
    public static WebApplication UseDarkSwaggerUi(
        this WebApplication app,
        IReadOnlyCollection<SwaggerDocumentEndpoint> documents,
        IReadOnlyCollection<SwaggerRequestRoute> requestRoutes)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(requestRoutes);

        if (documents.Count == 0)
        {
            throw new ArgumentException(
                "At least one OpenAPI document is required.",
                nameof(documents));
        }

        app.MapGet(
                ThemeRoute,
                () => Results.Text(DarkThemeCss, "text/css; charset=utf-8"))
            .ExcludeFromDescription();

        app.Use(async (context, next) =>
        {
            var isDocumentationRequest =
                HttpMethods.IsGet(context.Request.Method) ||
                HttpMethods.IsHead(context.Request.Method);

            if (isDocumentationRequest && context.Request.Path == "/")
            {
                context.Response.Redirect("/swagger/index.html", permanent: false);
                return;
            }

            await next(context);
        });

        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.DocumentTitle = $"{app.Environment.ApplicationName} - API documentation";
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableTryItOutByDefault();
            options.InjectStylesheet(ThemeRoute);

            foreach (var document in documents)
            {
                options.SwaggerEndpoint(document.Url, document.Name);
            }

            if (requestRoutes.Count > 0)
            {
                options.UseRequestInterceptor(CreateRequestInterceptor(requestRoutes));
            }
        });

        return app;
    }

    private static string CreateRequestInterceptor(
        IReadOnlyCollection<SwaggerRequestRoute> requestRoutes)
    {
        var routeMap = requestRoutes.ToDictionary(
            route => route.DestinationOrigin.TrimEnd('/').ToLowerInvariant(),
            route => route.GatewayPathPrefix.TrimEnd('/'),
            StringComparer.OrdinalIgnoreCase);
        var routeMapJson = JsonSerializer.Serialize(routeMap);

        return $$"""
            (request) => {
                const gatewayRoutes = {{routeMapJson}};
                const target = new URL(request.url, window.location.origin);
                const gatewayPrefix = gatewayRoutes[target.origin.toLowerCase()];

                if (gatewayPrefix !== undefined && target.origin !== window.location.origin) {
                    request.url = window.location.origin
                        + gatewayPrefix
                        + target.pathname
                        + target.search
                        + target.hash;
                }

                return request;
            }
            """;
    }

    private static string LoadDarkThemeCss()
    {
        var assembly = typeof(SwaggerUiExtensions).Assembly;
        using var stream = assembly.GetManifestResourceStream(ThemeResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded Swagger theme '{ThemeResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
