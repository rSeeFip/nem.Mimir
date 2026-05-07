namespace nem.Mimir.Infrastructure.MultiTenancy;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    private const string AuthorizationScheme = "Bearer ";
    private const string TenantIdClaimType = "tenant_id";
    private const string TenantNameClaimType = "tenant_name";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var authorizationHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith(AuthorizationScheme, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var token = authorizationHeader[AuthorizationScheme.Length..].Trim();
        JwtSecurityToken jwtToken;

        try
        {
            jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch (ArgumentException)
        {
            await next(context);
            return;
        }

        var tenantId = jwtToken.Claims
            .FirstOrDefault(c => string.Equals(c.Type, TenantIdClaimType, StringComparison.Ordinal))
            ?.Value;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Tenant claim is required.",
                Detail = "The authenticated request is missing the required tenant_id claim.",
            });
            return;
        }

        var tenantName = jwtToken.Claims
            .FirstOrDefault(c => string.Equals(c.Type, TenantNameClaimType, StringComparison.Ordinal))
            ?.Value;

        tenantContext.SetTenant(tenantId, tenantName);

        await next(context);
    }
}
