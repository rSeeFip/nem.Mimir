#nullable enable

using Microsoft.Extensions.Logging;
using nem.Contracts.Identity;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.Heartbeat;

/// <summary>
/// Mimir's organism heartbeat implementation.
/// Reports conversational health metrics (response latency, memory usage, session health)
/// as part of the organism heartbeat pulse.
/// </summary>
public sealed class MimirOrganismHeartbeat : IOrganismHeartbeat
{
    private readonly ILogger<MimirOrganismHeartbeat> _logger;
    private volatile bool _killSwitchActive;
    private string? _killSwitchReason;

    public MimirOrganismHeartbeat(ILogger<MimirOrganismHeartbeat> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TimeSpan PulseInterval => TimeSpan.FromSeconds(15);

    public Task<HeartbeatPulse> PulseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var statuses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nem.Mimir"] = _killSwitchActive ? "kill-switch-active" : "healthy",
            ["mimir-conversation"] = "operational",
            ["mimir-llm-routing"] = "operational",
            ["mimir-memory"] = "operational"
        };

        var autonomyLevels = new Dictionary<string, AutonomyLevel>(StringComparer.Ordinal)
        {
            ["nem.Mimir"] = AutonomyLevel.L1_Suggest
        };

        var pulse = new HeartbeatPulse(
            PulseId: OrganismHeartbeatId.New(),
            Timestamp: DateTimeOffset.UtcNow,
            ServiceStatuses: statuses,
            ServiceAutonomyLevels: autonomyLevels);

        _logger.LogDebug("Mimir heartbeat pulse emitted: {PulseId}", pulse.PulseId);
        return Task.FromResult(pulse);
    }

    public Task<OrganismHealth> AggregateHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var healthScore = _killSwitchActive ? 0.0 : 1.0;

        var serviceHealthScores = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["nem.Mimir"] = healthScore,
            ["mimir-conversation"] = healthScore,
            ["mimir-llm-routing"] = healthScore,
            ["mimir-memory"] = healthScore
        };

        var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kill_switch_active"] = _killSwitchActive.ToString(),
            ["kill_switch_reason"] = _killSwitchReason ?? "none",
            ["service_type"] = "conversational-ai"
        };

        var health = new OrganismHealth(
            Timestamp: DateTimeOffset.UtcNow,
            AggregateHealthScore: healthScore,
            ServiceHealthScores: serviceHealthScores,
            Diagnostics: diagnostics);

        return Task.FromResult(health);
    }

    public Task ActivateKillSwitchAsync(string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        cancellationToken.ThrowIfCancellationRequested();

        _killSwitchActive = true;
        _killSwitchReason = reason;

        _logger.LogWarning("Mimir kill switch activated: {Reason}", reason);
        return Task.CompletedTask;
    }

    public Task<bool> IsKillSwitchActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_killSwitchActive);
    }
}
