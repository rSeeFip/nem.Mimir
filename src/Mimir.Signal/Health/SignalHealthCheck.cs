namespace Mimir.Signal.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Mimir.Signal.Services;

internal sealed class SignalHealthCheck : IHealthCheck
{
    private readonly SignalApiClient _apiClient;
    private readonly ILogger<SignalHealthCheck> _logger;

    public SignalHealthCheck(
        SignalApiClient apiClient,
        ILogger<SignalHealthCheck> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _apiClient.CheckHealthAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("signal-cli-rest-api is reachable.")
                : HealthCheckResult.Unhealthy("signal-cli-rest-api is not reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signal health check failed");
            return HealthCheckResult.Unhealthy("Signal health check failed.", ex);
        }
    }
}
