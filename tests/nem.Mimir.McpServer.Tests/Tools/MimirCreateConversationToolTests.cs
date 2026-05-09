using NSubstitute;
using Shouldly;
using Wolverine;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirCreateConversationToolTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task CreateConversationAsync_SendsCommandAndReturnsResult()
    {
        var expected = new ConversationDto(
            Guid.NewGuid(), Guid.NewGuid(), null, false, null, [], "Test Conv", "Active",
            [], DateTimeOffset.UtcNow, null);

        _messageBus
            .InvokeAsync<ConversationDto>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirCreateConversationTool.CreateConversationAsync(
            "Test Conv", "You are helpful", "phi-4-mini", _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("Test Conv");

        await _messageBus.Received(1)
            .InvokeAsync<ConversationDto>(
                Arg.Is<CreateConversationCommand>(c =>
                    c.Title == "Test Conv" &&
                    c.SystemPrompt == "You are helpful" &&
                    c.Model == "phi-4-mini"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateConversationAsync_WithNullOptionalParams_Works()
    {
        var expected = new ConversationDto(
            Guid.NewGuid(), Guid.NewGuid(), null, false, null, [], "Bare Conv", "Active",
            [], DateTimeOffset.UtcNow, null);

        _messageBus
            .InvokeAsync<ConversationDto>(Arg.Any<CreateConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirCreateConversationTool.CreateConversationAsync(
            "Bare Conv", null, null, _messageBus, CancellationToken.None);

        result.ShouldContain("Bare Conv");

        await _messageBus.Received(1)
            .InvokeAsync<ConversationDto>(
                Arg.Is<CreateConversationCommand>(c =>
                    c.SystemPrompt == null && c.Model == null),
                Arg.Any<CancellationToken>());
    }
}
