#nullable enable

using nem.Contracts.Identity;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

internal sealed class MimirAutonomyGate : IAutonomyGate
{
    public Task<AutonomyLevel> GetAutonomyLevelAsync(
        string serviceName,
        string action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AutonomyLevel.L1_Suggest);
    }

    public Task<bool> RequestEscalationAsync(
        string serviceName,
        AutonomyLevel requestedLevel,
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task OverrideAsync(
        string serviceName,
        string action,
        AutonomyLevel level,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
