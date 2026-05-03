using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Sanitization;

namespace nem.Mimir.Api.Middleware;

public sealed class OutputSanitizationMiddleware
{
    private static readonly string[] _dangerousPatterns =
    [
        "<script", "</script>", "javascript:", "onerror=", "onload=",
        "'; DROP", "' OR '1'='1", "UNION SELECT", "--", "/*", "*/"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<OutputSanitizationMiddleware> _logger;
    private readonly SanitizationOptions _options;

    public OutputSanitizationMiddleware(
        RequestDelegate next,
        ILogger<OutputSanitizationMiddleware> logger,
        IOptions<SanitizationOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, ISanitizationService sanitizationService)
    {
        if (context.Request.Method is not ("POST" or "PUT"))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var isMessagePath =
            path.Contains("message", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("hubs/chat", StringComparison.OrdinalIgnoreCase);

        if (!isMessagePath)
        {
            await _next(context);
            return;
        }

        var channel = ResolveChannel(path);
        var mode = _options.EnforcementEnabled
            ? _options.GetModeForChannel(channel)
            : SanitizationMode.Log;

        var suspiciousParams = context.Request.Query
            .Where(q => sanitizationService.ContainsSuspiciousPatterns(q.Value.ToString()))
            .ToList();

        foreach (var param in suspiciousParams)
        {
            _logger.LogWarning(
                "Suspicious pattern detected in query parameter '{Parameter}' on {Method} {Path}",
                param.Key,
                context.Request.Method,
                context.Request.Path);
        }

        if (suspiciousParams.Count == 0 || !_options.EnforcementEnabled)
        {
            await _next(context);
            return;
        }

        switch (mode)
        {
            case SanitizationMode.Block:
                await WriteBlockResponseAsync(context);
                return;

            case SanitizationMode.Sanitize:
                SanitizeQueryString(context);
                await _next(context);
                return;

            default:
                await _next(context);
                return;
        }
    }

    private static string ResolveChannel(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment.Equals("telegram", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("slack", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("discord", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("web", StringComparison.OrdinalIgnoreCase))
            {
                return segment.ToLowerInvariant();
            }
        }

        return "default";
    }

    private static async Task WriteBlockResponseAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Injection attempt detected",
            Detail = "The request contains suspicious patterns and has been blocked.",
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problem);
        await context.Response.WriteAsync(json, Encoding.UTF8);
    }

    private static void SanitizeQueryString(HttpContext context)
    {
        var sanitized = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in context.Request.Query)
        {
            var clean = StripDangerousPatterns(value.ToString());
            sanitized[key] = clean;
        }

        context.Request.QueryString = QueryString.Create(sanitized);
    }

    private static string StripDangerousPatterns(string input)
    {
        var result = input;
        foreach (var pattern in _dangerousPatterns)
        {
            result = result.Replace(pattern, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
