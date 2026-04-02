#nullable enable

using Microsoft.Extensions.Logging;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.Handlers;

/// <summary>
/// Handles cross-service <see cref="HeartbeatPulse"/> messages.
/// Mimir participates in organism heartbeat by receiving pulse broadcasts
/// and logging service-level health awareness.
/// </summary>
public static class HeartbeatPulseHandler
{
    public static Task Handle(
        HeartbeatPulse @event,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(HeartbeatPulseHandler));

        logger.LogDebug(
            "Heartbeat pulse received: {PulseId} at {Timestamp} with {ServiceCount} services reporting",
            @event.PulseId,
            @event.Timestamp,
            @event.ServiceStatuses.Count);

        return Task.CompletedTask;
    }
}
