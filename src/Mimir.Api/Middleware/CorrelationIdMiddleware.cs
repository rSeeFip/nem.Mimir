using System.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace Mimir.Api.Middleware;

/// <summary>
/// Middleware that adds a correlation ID to each request for distributed tracing.
/// The correlation ID is:
/// - Read from incoming request headers (X-Correlation-ID or X-Request-ID)
/// - Generated if not present
/// - Added to response headers
/// - Logged with all structured log events
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string RequestIdHeader = "X-Request-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Set correlation ID as Activity baggage for distributed tracing
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetBaggage("nem.correlation_id", correlationId);
        }

        // Add correlation ID to the response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Add correlation ID to the logging context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Also add the trace ID for distributed tracing compatibility
            if (activity != null)
            {
                using (LogContext.PushProperty("TraceId", activity.TraceId.ToString()))
                {
                    await _next(context);
                }
            }
            else
            {
                await _next(context);
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check for existing correlation ID in headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        if (context.Request.Headers.TryGetValue(RequestIdHeader, out var requestId) &&
            !string.IsNullOrWhiteSpace(requestId))
        {
            return requestId.ToString();
        }

        // Generate a new correlation ID
        return Guid.NewGuid().ToString("N");
    }
}

/// <summary>
/// Extension methods for adding correlation ID middleware.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds middleware that adds correlation IDs to requests for distributed tracing.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
