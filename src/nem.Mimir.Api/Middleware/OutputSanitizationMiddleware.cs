using nem.Mimir.Application.Common.Sanitization;

namespace nem.Mimir.Api.Middleware;

/// <summary>
/// Lightweight middleware that logs suspicious patterns detected in incoming requests.
/// Actual sanitization is performed at the application layer via <see cref="ISanitizationService"/>
/// calls in command handlers and the ChatHub — not by rewriting request/response bodies.
/// </summary>
public sealed class OutputSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OutputSanitizationMiddleware> _logger;

    public OutputSanitizationMiddleware(
        RequestDelegate next,
        ILogger<OutputSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISanitizationService sanitizationService)
    {
        // For POST/PUT requests to message-related endpoints, check query string and
        // form values for suspicious patterns (lightweight — does NOT read request body).
        if (context.Request.Method is "POST" or "PUT")
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.Contains("message", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("hubs/chat", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var queryParam in context.Request.Query)
                {
                    if (sanitizationService.ContainsSuspiciousPatterns(queryParam.Value.ToString()))
                    {
                        _logger.LogWarning(
                            "Suspicious pattern detected in query parameter '{Parameter}' on {Method} {Path}",
                            queryParam.Key,
                            context.Request.Method,
                            context.Request.Path);
                    }
                }
            }
        }

        await _next(context);
    }
}
