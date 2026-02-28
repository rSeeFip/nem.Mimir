using Mimir.Sync.Messages;

namespace Mimir.Sync.Publishers;

/// <summary>
/// Provides typed publish methods for Mimir Wolverine messages.
/// </summary>
public interface IMimirEventPublisher
{
    /// <summary>
    /// Publishes a chat request received event.
    /// </summary>
    /// <param name="message">The chat request event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishChatRequestAsync(ChatRequestReceived message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a chat completed event with token usage metrics.
    /// </summary>
    /// <param name="message">The chat completed event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishChatCompletedAsync(ChatCompleted message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message-sent event.
    /// </summary>
    /// <param name="message">The message-sent event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishMessageSentAsync(MessageSent message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a conversation-created event.
    /// </summary>
    /// <param name="message">The conversation-created event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishConversationCreatedAsync(ConversationCreated message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a generic audit event.
    /// </summary>
    /// <param name="message">The audit event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAuditEventAsync(AuditEventPublished message, CancellationToken cancellationToken = default);
}
