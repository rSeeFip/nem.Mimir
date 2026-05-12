using System.Diagnostics;
using nem.Contracts.Federation;
using nem.Contracts.Identity;
using nem.Contracts.Interfaces;
using nem.Mimir.Infrastructure.Federation;

namespace nem.Mimir.Api.Middleware;

public sealed class FederationContextMiddleware(
    MutableFederationContextAccessor accessor,
    ITenantContext tenantContext) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var orgId = ResolveOrganizationId(context);
        var instanceId = ResolveInstanceId(context);
        var correlationId = ResolveCorrelationId(context);

        accessor.SetContext(new FederationTenantContext(orgId, instanceId, correlationId));

        await next(context);
    }

    private static OrganizationId ResolveOrganizationId(HttpContext context)
    {
        var header = context.Request.Headers["X-Organization-Id"].FirstOrDefault();
        if (OrganizationId.TryParse(header, null, out var fromHeader))
        {
            return fromHeader;
        }

        var claim = context.User.FindFirst("org")?.Value;
        if (OrganizationId.TryParse(claim, null, out var fromClaim))
        {
            return fromClaim;
        }

        return OrganizationId.Empty;
    }

    private InstanceId ResolveInstanceId(HttpContext context)
    {
        var header = context.Request.Headers["X-Instance-Id"].FirstOrDefault();
        if (InstanceId.TryParse(header, null, out var fromHeader))
        {
            return fromHeader;
        }

        if (InstanceId.TryParse(tenantContext.TenantId, null, out var fromTenant))
        {
            return fromTenant;
        }

        return InstanceId.Empty;
    }

    private static CorrelationId ResolveCorrelationId(HttpContext context)
    {
        var header = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (CorrelationId.TryParse(header, null, out var fromHeader))
        {
            return fromHeader;
        }

        var traceId = Activity.Current?.TraceId.ToString();
        if (Guid.TryParse(traceId, out var traceGuid))
        {
            return new CorrelationId(traceGuid);
        }

        return CorrelationId.Empty;
    }
}
