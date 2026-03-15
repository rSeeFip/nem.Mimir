using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;
using nem.Mimir.Application.Telemetry;

namespace nem.Mimir.Api.Telemetry;

/// <summary>
/// Extension methods for configuring OpenTelemetry distributed tracing in nem.Mimir.Api.
/// </summary>
public static class TelemetryConfiguration
{
    /// <summary>
    /// Adds OpenTelemetry tracing and metrics to the service collection.
    /// Configures:
    /// - nem.Mimir ActivitySource for application tracing
    /// - Wolverine messaging traces
    /// - ASP.NET Core instrumentation
    /// - OTLP exporter (Aspire auto-configures OTEL_EXPORTER_OTLP_ENDPOINT)
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddNemTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    // Add nem.Mimir activity source
                    .AddSource(NemActivitySource.Source.Name)
                    // Add Wolverine messaging source
                    .AddSource("Wolverine")
                    // Add ASP.NET Core instrumentation (HTTP requests, SignalR, etc.)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Record request/response headers for correlation
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request_content_length", request.ContentLength);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response_content_length", response.ContentLength);
                        };
                    })
                    // Add OTLP exporter (Aspire auto-configures OTEL_EXPORTER_OTLP_ENDPOINT)
                    .AddOtlpExporter();
            });

        return services;
    }
}
