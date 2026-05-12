namespace nem.Mimir.Infrastructure.LiteLlm;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

public sealed class LiteLlmHealthCheck(
    IHttpClientFactory httpClientFactory,
    ILogger<LiteLlmHealthCheck> logger) : IHealthCheck
{
    internal const string HttpClientName = "LiteLlmHealth";
    private const string ModelsEndpoint = "/v1/models";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(ModelsEndpoint, cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("LiteLLM is reachable.")
                : HealthCheckResult.Unhealthy(
                    $"LiteLLM responded with status code {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LiteLLM health check failed");
            return HealthCheckResult.Unhealthy("LiteLLM health check failed.", ex);
        }
    }
}
