using Mimir.Application.ChannelEvents;
using Microsoft.Extensions.Logging;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using NSubstitute;
using Shouldly;
using IChannelMessageSender = Mimir.Application.ChannelEvents.IChannelMessageSender;

namespace Mimir.Application.Tests.ChannelEvents;

public sealed class IngestChannelEventHandlerTests
{
    private readonly ILogger<IngestChannelEventHandler> _logger;
    private readonly IngestChannelEventHandler _handler;

    public IngestChannelEventHandlerTests()
    {
        _logger = Substitute.For<ILogger<IngestChannelEventHandler>>();
        _handler = new IngestChannelEventHandler(_logger);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnAcceptedResultWithEventId()
    {
        // Arrange
        var content = Substitute.For<IContentPayload>();
        var command = new IngestChannelEventCommand(
            ChannelType.Telegram,
            "ext-channel-1",
            "ext-user-1",
            content,
            DateTimeOffset.UtcNow);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue();
        result.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_MultipleInvocations_ShouldReturnUniqueEventIds()
    {
        // Arrange
        var content = Substitute.For<IContentPayload>();
        var command = new IngestChannelEventCommand(
            ChannelType.Teams,
            "ext-channel-2",
            "ext-user-2",
            content,
            DateTimeOffset.UtcNow);

        // Act
        var result1 = await _handler.Handle(command, CancellationToken.None);
        var result2 = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result1.EventId.ShouldNotBe(result2.EventId);
    }
}

public sealed class SendChannelMessageHandlerTests
{
    private readonly IChannelMessageSender _sender;
    private readonly ChannelEventRouter _router;
    private readonly ILogger<SendChannelMessageHandler> _logger;
    private readonly SendChannelMessageHandler _handler;

    public SendChannelMessageHandlerTests()
    {
        _sender = Substitute.For<IChannelMessageSender>();
        _sender.Channel.Returns(ChannelType.Telegram);

        _router = new ChannelEventRouter([_sender]);
        _logger = Substitute.For<ILogger<SendChannelMessageHandler>>();
        _handler = new SendChannelMessageHandler(_router, _logger);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldDelegateToCorrectSender()
    {
        // Arrange
        var content = Substitute.For<IContentPayload>();
        var expectedResult = new SendChannelMessageResult(
            true,
            new ChannelMessageRef(ChannelType.Telegram, "msg-123"),
            null);

        _sender.SendAsync("ext-channel-1", content, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var command = new SendChannelMessageCommand(
            ChannelType.Telegram,
            "ext-channel-1",
            content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
        await _sender.Received(1).SendAsync("ext-channel-1", content, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SenderReturnsFailure_ShouldPropagateResult()
    {
        // Arrange
        var content = Substitute.For<IContentPayload>();
        var failResult = new SendChannelMessageResult(false, null, "Channel unavailable");

        _sender.SendAsync("ext-channel-1", content, Arg.Any<CancellationToken>())
            .Returns(failResult);

        var command = new SendChannelMessageCommand(
            ChannelType.Telegram,
            "ext-channel-1",
            content);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Channel unavailable");
    }
}

public sealed class ChannelEventRouterTests
{
    [Fact]
    public void GetSender_RegisteredChannel_ShouldReturnCorrectSender()
    {
        // Arrange
        var telegramSender = Substitute.For<IChannelMessageSender>();
        telegramSender.Channel.Returns(ChannelType.Telegram);

        var teamsSender = Substitute.For<IChannelMessageSender>();
        teamsSender.Channel.Returns(ChannelType.Teams);

        var router = new ChannelEventRouter([telegramSender, teamsSender]);

        // Act
        var result = router.GetSender(ChannelType.Telegram);

        // Assert
        result.ShouldBe(telegramSender);
    }

    [Fact]
    public void GetSender_UnregisteredChannel_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var telegramSender = Substitute.For<IChannelMessageSender>();
        telegramSender.Channel.Returns(ChannelType.Telegram);

        var router = new ChannelEventRouter([telegramSender]);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => router.GetSender(ChannelType.WhatsApp));
        ex.Message.ShouldContain("WhatsApp");
    }

    [Fact]
    public void Constructor_NoSenders_ShouldCreateEmptyRouter()
    {
        // Arrange & Act
        var router = new ChannelEventRouter([]);

        // Assert — any GetSender call should throw
        Should.Throw<InvalidOperationException>(() => router.GetSender(ChannelType.Telegram));
    }

    [Fact]
    public void GetSender_MultipleSenders_ShouldResolveEachCorrectly()
    {
        // Arrange
        var telegramSender = Substitute.For<IChannelMessageSender>();
        telegramSender.Channel.Returns(ChannelType.Telegram);

        var teamsSender = Substitute.For<IChannelMessageSender>();
        teamsSender.Channel.Returns(ChannelType.Teams);

        var whatsAppSender = Substitute.For<IChannelMessageSender>();
        whatsAppSender.Channel.Returns(ChannelType.WhatsApp);

        var router = new ChannelEventRouter([telegramSender, teamsSender, whatsAppSender]);

        // Act & Assert
        router.GetSender(ChannelType.Telegram).ShouldBe(telegramSender);
        router.GetSender(ChannelType.Teams).ShouldBe(teamsSender);
        router.GetSender(ChannelType.WhatsApp).ShouldBe(whatsAppSender);
    }
}
