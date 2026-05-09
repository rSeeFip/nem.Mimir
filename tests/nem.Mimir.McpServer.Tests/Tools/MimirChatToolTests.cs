using NSubstitute;
using Shouldly;
using Wolverine;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirChatToolTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task ChatAsync_SendsCommandAndReturnsSerializedResult()
    {
        var conversationId = Guid.NewGuid();
        var expected = new MessageDto(
            Guid.NewGuid(), conversationId, "Assistant", "Hello!", "phi-4-mini", 10, DateTimeOffset.UtcNow,
            null, 0, false, [], []);

        _messageBus
            .InvokeAsync<MessageDto>(Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirChatTool.ChatAsync(conversationId, "Hi", "phi-4-mini", _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("Hello!");
        result.ShouldContain(conversationId.ToString());

        await _messageBus.Received(1)
            .InvokeAsync<MessageDto>(
                Arg.Is<SendMessageCommand>(c =>
                    c.ConversationId == conversationId &&
                    c.Content == "Hi" &&
                    c.Model == "phi-4-mini"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatAsync_WithNullModel_PassesNullModel()
    {
        var conversationId = Guid.NewGuid();
        var expected = new MessageDto(
            Guid.NewGuid(), conversationId, "Assistant", "Response", null, null, DateTimeOffset.UtcNow,
            null, 0, false, [], []);

        _messageBus
            .InvokeAsync<MessageDto>(Arg.Any<SendMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirChatTool.ChatAsync(conversationId, "Test", null, _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();

        await _messageBus.Received(1)
            .InvokeAsync<MessageDto>(
                Arg.Is<SendMessageCommand>(c => c.Model == null),
                Arg.Any<CancellationToken>());
    }
}
