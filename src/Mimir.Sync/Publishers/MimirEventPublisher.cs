using Mimir.Sync.Messages;
using Wolverine;

namespace Mimir.Sync.Publishers;

/// <summary>
/// Wolverine-backed implementation of <see cref="IMimirEventPublisher"/>
/// that publishes messages via the <see cref="IMessageBus"/>.
/// </summary>
public sealed class MimirEventPublisher : IMimirEventPublisher
{
    private readonly IMessageBus _messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="MimirEventPublisher"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus for publishing messages.</param>
    public MimirEventPublisher(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    /// <inheritdoc />
    public async Task PublishChatRequestAsync(ChatRequestReceived message, CancellationToken cancellationToken)
    {
        await _messageBus.PublishAsync(message);
    }

    /// <inheritdoc />
    public async Task PublishChatCompletedAsync(ChatCompleted message, CancellationToken cancellationToken)
    {
        await _messageBus.PublishAsync(message);
    }

    /// <inheritdoc />
    public async Task PublishMessageSentAsync(MessageSent message, CancellationToken cancellationToken)
    {
        await _messageBus.PublishAsync(message);
    }

    /// <inheritdoc />
    public async Task PublishConversationCreatedAsync(ConversationCreated message, CancellationToken cancellationToken)
    {
        await _messageBus.PublishAsync(message);
    }

    /// <inheritdoc />
    public async Task PublishAuditEventAsync(AuditEventPublished message, CancellationToken cancellationToken)
    {
        await _messageBus.PublishAsync(message);
    }
}
