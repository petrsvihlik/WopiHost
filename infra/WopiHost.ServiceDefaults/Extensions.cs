using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("WopiHost.Core")
                    .AddMeter("WopiHost.Web")
                    .AddMeter("WopiHost.Discovery");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        // Enrich spans with additional HTTP request information
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.header.user_agent", request.Headers.UserAgent.ToString());
                            
                            if (request.Headers.TryGetValue("X-WOPI-Override", out var wopiOverride))
                                activity.SetTag("http.request.header.wopi_override", wopiOverride.ToString());
                                
                            if (request.Headers.TryGetValue("X-WOPI-CorrelationID", out var wopiCorrelationId))
                                activity.SetTag("http.request.header.wopi_correlationid", wopiCorrelationId.ToString());
                        };
                        
                        // Enrich spans with HTTP response information
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            if (response.Headers.TryGetValue("X-WOPI-CorrelationID", out var wopiCorrelationId))
                                activity.SetTag("http.response.header.wopi_correlationid", wopiCorrelationId.ToString());
                        };
                        
                        // Filter out health check requests from traces
                        options.Filter = httpContext =>
                        {
                            return !httpContext.Request.Path.StartsWithSegments("/health") &&
                                   !httpContext.Request.Path.StartsWithSegments("/alive");
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        // Enrich HTTP client spans with WOPI-specific information
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            if (request.Headers.Contains("X-WOPI-Override"))
                            {
                                activity.SetTag("wopi.operation", request.Headers.GetValues("X-WOPI-Override").FirstOrDefault());
                            }
                        };
                        
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            if (response.Headers.Contains("X-WOPI-CorrelationID"))
                            {
                                activity.SetTag("wopi.correlation_id", response.Headers.GetValues("X-WOPI-CorrelationID").FirstOrDefault());
                            }
                        };
                    })
                    // Add custom activity sources for WOPI operations
                    .AddSource("WopiHost.Core")
                    .AddSource("WopiHost.Web")
                    .AddSource("WopiHost.Discovery")
                    .AddSource("WopiHost.FileSystem");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
