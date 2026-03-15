using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Teams.Configuration;

namespace nem.Mimir.Teams.Health;

internal sealed class TeamsHealthCheck(
    IOptions<TeamsSettings> settings,
    ILogger<TeamsHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.AppId))
        {
            logger.LogWarning("Teams App ID is not configured");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Teams App ID is not configured."));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy("Teams adapter configuration is present."));
    }
}
