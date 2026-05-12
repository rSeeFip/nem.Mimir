using nem.Contracts.Organism;

namespace nem.Mimir.Api.Federation;

public sealed class MimirFederationPeerHealthUpdateHandler
{
    public Task Handle(
        FederationPeerHealthUpdateEvent healthUpdate,
        MimirFederationPeerHealthState state,
        ILogger<MimirFederationPeerHealthUpdateHandler> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(healthUpdate);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();

        state.Update(healthUpdate);
        logger.LogInformation(
            "Recorded federation peer health update for {PeerServiceId} with status {HealthStatus} and score {HealthScore}.",
            healthUpdate.PeerServiceId,
            healthUpdate.HealthStatus,
            healthUpdate.HealthScore);

        return Task.CompletedTask;
    }
}
