using nem.Contracts.Federation;
using nem.Contracts.Identity;
using nem.Mimir.Infrastructure.Federation;

namespace nem.Mimir.Api.Middleware;

public sealed class TenantContextMiddleware(MutableFederationContextAccessor accessor) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantId = context.User.FindFirst("tid")?.Value
            ?? context.User.FindFirst("tenant_id")?.Value;

        if (!string.IsNullOrWhiteSpace(tenantId) && InstanceId.TryParse(tenantId, null, out var instanceId))
        {
            accessor.SetContext(new FederationTenantContext(OrganizationId.Empty, instanceId, CorrelationId.Empty));
        }

        await next(context);
    }
}
