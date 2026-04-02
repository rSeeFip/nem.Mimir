#nullable enable

using Microsoft.Extensions.Logging;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.Handlers;

/// <summary>
/// Handles cross-service <see cref="OrganismStateChangedEvent"/> messages.
/// Logs state transitions and adjusts Mimir behavior when the organism
/// signals subsystem state changes that affect conversational AI operations.
/// </summary>
public static class OrganismStateChangedHandler
{
    public static Task Handle(
        OrganismStateChangedEvent @event,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(OrganismStateChangedHandler));

        logger.LogInformation(
            "Organism state changed: {Subsystem} transitioned from {Previous} to {Current} (source: {Source}, correlation: {Correlation})",
            @event.SubsystemName,
            @event.PreviousState,
            @event.CurrentState,
            @event.SourceServiceId,
            @event.CorrelationId);

        // Mimir can react to organism-wide state changes here.
        // For example, if LLM providers degrade, Mimir might throttle
        // conversation throughput or switch to fallback models.

        return Task.CompletedTask;
    }
}
