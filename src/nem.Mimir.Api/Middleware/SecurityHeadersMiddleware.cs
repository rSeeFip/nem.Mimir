namespace nem.Mimir.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var h = ((HttpContext)state).Response.Headers;
            h.Remove("Server");
            h.Remove("X-Powered-By");
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            h["X-XSS-Protection"] = "0";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Content-Security-Policy"] = "default-src 'self'";
            h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            return Task.CompletedTask;
        }, context);

        await _next(context);
    }
}
