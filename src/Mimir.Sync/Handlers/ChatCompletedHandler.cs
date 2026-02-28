using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Sync.Messages;

namespace Mimir.Sync.Handlers;

/// <summary>
/// Handles <see cref="ChatCompleted"/> messages by logging token usage metrics to the audit trail.
/// </summary>
internal sealed class ChatCompletedHandler
{
    /// <summary>
    /// Processes a chat completion event, recording model usage and latency metrics.
    /// </summary>
    /// <param name="message">The chat completion event to process.</param>
    /// <param name="auditService">The audit service for persisting the audit entry.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task Handle(
        ChatCompleted message,
        IAuditService auditService,
        ILogger<ChatCompletedHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Chat completed for conversation {ConversationId}, model {Model}: {PromptTokens}+{CompletionTokens} tokens in {Duration}ms",
            message.ConversationId,
            message.Model,
            message.PromptTokens,
            message.CompletionTokens,
            message.Duration.TotalMilliseconds);

        var details = JsonSerializer.Serialize(new
        {
            message.Model,
            message.PromptTokens,
            message.CompletionTokens,
            DurationMs = message.Duration.TotalMilliseconds
        });

        await auditService.LogAsync(
            Guid.Empty,
            "ChatCompleted",
            "Conversation",
            message.ConversationId.ToString(),
            details,
            cancellationToken);
    }
}
