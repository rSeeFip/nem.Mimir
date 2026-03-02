using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Sync.Messages;
using Mimir.Domain.ValueObjects;

namespace Mimir.Sync.Handlers;

/// <summary>
/// Handles <see cref="AuditEventPublished"/> messages by persisting them via the audit service.
/// </summary>
internal sealed class AuditEventHandler
{
    /// <summary>
    /// Processes a generic audit event, persisting it through the audit service.
    /// </summary>
    /// <param name="message">The audit event to persist.</param>
    /// <param name="auditService">The audit service for persisting the audit entry.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task Handle(
        AuditEventPublished message,
        IAuditService auditService,
        ILogger<AuditEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Audit event: {Action} on {ResourceType}/{ResourceId} by user {UserId}",
            message.Action,
            message.ResourceType,
            message.ResourceId,
            message.UserId);

        await auditService.LogAsync(
            UserId.From(message.UserId),
            message.Action,
            message.ResourceType,
            message.ResourceId.ToString(),
            message.Details,
            cancellationToken);
    }
}
