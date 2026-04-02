#nullable enable

using Microsoft.Extensions.Logging;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.Handlers;

/// <summary>
/// Handles cross-service <see cref="OrganismSubsystemAlertEvent"/> messages.
/// Logs alerts from other organism subsystems that may affect Mimir operations,
/// such as LLM provider outages or knowledge base degradation.
/// </summary>
public static class OrganismSubsystemAlertHandler
{
    public static Task Handle(
        OrganismSubsystemAlertEvent @event,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(OrganismSubsystemAlertHandler));

        logger.LogWarning(
            "Organism subsystem alert: [{Severity}] {AlertCode} from {Subsystem} — {Message} (source: {Source}, correlation: {Correlation})",
            @event.Severity,
            @event.AlertCode,
            @event.SubsystemName,
            @event.Message,
            @event.SourceServiceId,
            @event.CorrelationId);

        return Task.CompletedTask;
    }
}
