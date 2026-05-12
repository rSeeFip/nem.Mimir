using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.AspNetCore.Messaging.Federation;
using nem.Contracts.Federation;
using Wolverine;

namespace nem.Mimir.Infrastructure.Messaging;

public sealed class FederationHeaderPropagation(IHttpContextAccessor httpContextAccessor) : IEnvelopeRule
{
    public const string OrganizationIdHeader = FederationCorrelationHeaderNames.SourceTenantId;
    public const string InstanceIdHeader = FederationCorrelationHeaderNames.TargetTenantId;
    public const string CorrelationIdHeader = FederationCorrelationHeaderNames.CorrelationId;

    public void Modify(Envelope envelope)
    {
        var accessor = httpContextAccessor.HttpContext?.RequestServices
            .GetService<IFederationContextAccessor>();

        if (accessor is null)
        {
            return;
        }

        var ctx = accessor.Current;
        if (ctx == FederationTenantContext.Empty)
        {
            return;
        }

        if (!ctx.OrganizationId.IsEmpty)
        {
            envelope.Headers[OrganizationIdHeader] = ctx.OrganizationId.ToString();
        }

        if (!ctx.InstanceId.IsEmpty)
        {
            envelope.Headers[InstanceIdHeader] = ctx.InstanceId.ToString();
        }

        if (!ctx.CorrelationId.IsEmpty)
        {
            envelope.Headers[CorrelationIdHeader] = ctx.CorrelationId.ToString();
        }
    }
}
