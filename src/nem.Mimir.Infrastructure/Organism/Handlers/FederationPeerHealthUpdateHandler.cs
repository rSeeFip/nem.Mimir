#nullable enable

using Microsoft.Extensions.Logging;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.Handlers;

/// <summary>
/// Handles cross-service <see cref="FederationPeerHealthUpdateEvent"/> messages.
/// Mimir uses peer health information for awareness of federation-wide
/// conversational service availability.
/// </summary>
public static class FederationPeerHealthUpdateHandler
{
    public static Task Handle(
        FederationPeerHealthUpdateEvent @event,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(FederationPeerHealthUpdateHandler));

        logger.LogInformation(
            "Federation peer health update: {PeerId} health={HealthScore:F2} status={Status} (source: {Source})",
            @event.PeerServiceId,
            @event.HealthScore,
            @event.HealthStatus,
            @event.SourceServiceId);

        return Task.CompletedTask;
    }
}
