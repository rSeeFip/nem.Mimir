using NSubstitute;
using Shouldly;
using Wolverine;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirForkConversationToolTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task ForkConversationAsync_SendsCommandAndReturnsResult()
    {
        var sourceId = Guid.NewGuid();
        var forkedId = Guid.NewGuid();
        var expected = new ConversationDto(
            forkedId, Guid.NewGuid(), null, false, null, [], "Forked Conv", "Active",
            [], DateTimeOffset.UtcNow, null);

        _messageBus
            .InvokeAsync<ConversationDto>(Arg.Any<ForkConversationCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirForkConversationTool.ForkConversationAsync(
            sourceId, "Exploring alternative approach", _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain(forkedId.ToString());

        await _messageBus.Received(1)
            .InvokeAsync<ConversationDto>(
                Arg.Is<ForkConversationCommand>(c =>
                    c.ConversationId == sourceId &&
                    c.ForkReason == "Exploring alternative approach"),
                Arg.Any<CancellationToken>());
    }
}
