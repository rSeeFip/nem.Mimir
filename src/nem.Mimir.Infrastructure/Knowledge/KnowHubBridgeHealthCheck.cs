namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using nem.Mimir.Application.Knowledge;

internal sealed class KnowHubBridgeHealthCheck(IKnowHubBridge bridge) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            bridge.IsAvailable
                ? HealthCheckResult.Healthy("KnowHub bridge is available.")
                : HealthCheckResult.Degraded("KnowHub bridge is currently unavailable; fallback mode active."));
    }
}
