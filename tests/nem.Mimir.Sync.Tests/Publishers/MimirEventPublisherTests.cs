using nem.Mimir.Sync.Messages;
using nem.Mimir.Sync.Publishers;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Sync.Tests.Publishers;

public sealed class MimirEventPublisherTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly MimirEventPublisher _sut;

    public MimirEventPublisherTests()
    {
        _sut = new MimirEventPublisher(_messageBus);
    }

    [Fact]
    public async Task PublishChatRequestAsync_ShouldPublishToMessageBus()
    {
        var message = new ChatRequestReceived(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "qwen-2.5-72b",
            "Hello, how are you?",
            DateTimeOffset.UtcNow);

        await _sut.PublishChatRequestAsync(message, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(message);
    }

    [Fact]
    public async Task PublishChatCompletedAsync_ShouldPublishToMessageBus()
    {
        var message = new ChatCompleted(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "phi-4-mini",
            100,
            50,
            TimeSpan.FromMilliseconds(500),
            DateTimeOffset.UtcNow);

        await _sut.PublishChatCompletedAsync(message, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(message);
    }

    [Fact]
    public async Task PublishMessageSentAsync_ShouldPublishToMessageBus()
    {
        var message = new MessageSent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user",
            DateTimeOffset.UtcNow);

        await _sut.PublishMessageSentAsync(message, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(message);
    }

    [Fact]
    public async Task PublishConversationCreatedAsync_ShouldPublishToMessageBus()
    {
        var message = new ConversationCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Conversation",
            DateTimeOffset.UtcNow);

        await _sut.PublishConversationCreatedAsync(message, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(message);
    }

    [Fact]
    public async Task PublishAuditEventAsync_ShouldPublishToMessageBus()
    {
        var message = new AuditEventPublished(
            Guid.NewGuid(),
            "UserLoggedIn",
            "Session",
            Guid.NewGuid(),
            "{\"ip\":\"127.0.0.1\"}",
            DateTimeOffset.UtcNow);

        await _sut.PublishAuditEventAsync(message, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(message);
    }
}
