using System.Text.Json;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Sync.Messages;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Sync.Handlers;

/// <summary>
/// Handles <see cref="MessageSent"/> messages by publishing an audit trail entry.
/// </summary>
internal sealed class MessageSentHandler
{
    /// <summary>
    /// Processes a message-sent event, recording who sent a message in which conversation.
    /// </summary>
    /// <param name="message">The message-sent event to process.</param>
    /// <param name="auditService">The audit service for persisting the audit entry.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task Handle(
        MessageSent message,
        IAuditService auditService,
        ILogger<MessageSentHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Message sent in conversation {ConversationId} by user {UserId} with role {Role}",
            message.ConversationId,
            message.UserId,
            message.Role);

        var details = JsonSerializer.Serialize(new
        {
            message.Role,
            message.ConversationId
        });

        await auditService.LogAsync(
            UserId.From(message.UserId),
            "MessageSent",
            "Message",
            message.MessageId.ToString(),
            details,
            cancellationToken);
    }
}
