namespace Mimir.WhatsApp.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.WhatsApp.Configuration;

internal sealed class WhatsAppHealthCheck(
    IOptions<WhatsAppSettings> settings,
    ILogger<WhatsAppHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.Value.AccessToken))
                return Task.FromResult(HealthCheckResult.Unhealthy("WhatsApp access token is not configured."));

            if (string.IsNullOrWhiteSpace(settings.Value.PhoneNumberId))
                return Task.FromResult(HealthCheckResult.Degraded("WhatsApp phone number ID is not configured."));

            if (string.IsNullOrWhiteSpace(settings.Value.VerifyToken))
                return Task.FromResult(HealthCheckResult.Degraded("WhatsApp webhook verify token is not configured."));

            return Task.FromResult(HealthCheckResult.Healthy("WhatsApp adapter is configured."));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("WhatsApp health check failed.", ex));
        }
    }
}
