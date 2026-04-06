#nullable enable

using Microsoft.Extensions.Logging;
using nem.Contracts.Identity;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

internal sealed class MimirHomeostasisAgent(ILogger<MimirHomeostasisAgent> logger) : IHomeostasisAgent
{
    public Task<OrganismHealth> MonitorHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var health = new OrganismHealth(
            Timestamp: DateTimeOffset.UtcNow,
            AggregateHealthScore: 1.0,
            ServiceHealthScores: new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["nem.Mimir"] = 1.0,
                ["mimir-mapek-engine"] = 1.0,
            },
            Diagnostics: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = "nominal",
            });

        return Task.FromResult(health);
    }

    public Task<HomeostasisCorrectionId> TriggerCorrectionAsync(
        string reason,
        IReadOnlyDictionary<string, double> metrics,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(metrics);
        cancellationToken.ThrowIfCancellationRequested();

        var correctionId = HomeostasisCorrectionId.New();

        logger.LogInformation(
            "Mimir homeostasis correction triggered: {CorrectionId}, reason={Reason}, metrics={Metrics}",
            correctionId,
            reason,
            string.Join(", ", metrics.Select(kv => $"{kv.Key}={kv.Value}")));

        return Task.FromResult(correctionId);
    }

    public Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("mimir-homeostasis: nominal");
    }
}
