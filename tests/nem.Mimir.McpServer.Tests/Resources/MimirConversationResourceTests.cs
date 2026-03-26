using NSubstitute;
using Shouldly;
using Wolverine;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using nem.Mimir.McpServer.Resources;

namespace nem.Mimir.McpServer.Tests.Resources;

public sealed class MimirConversationResourceTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task GetConversationAsync_ReturnsSerializedConversation()
    {
        var conversationId = Guid.NewGuid();
        var messages = new List<MessageDto>
        {
            new(Guid.NewGuid(), conversationId, "User", "Hello", null, null, DateTimeOffset.UtcNow,
                null, 0, false, []),
            new(Guid.NewGuid(), conversationId, "Assistant", "Hi there!", "phi-4-mini", 5, DateTimeOffset.UtcNow,
                null, 0, false, [])
        };
        var expected = new ConversationDto(
            conversationId, Guid.NewGuid(), null, false, null, [], "Test", "Active", messages, DateTimeOffset.UtcNow, null);

        _messageBus
            .InvokeAsync<ConversationDto>(Arg.Any<GetConversationByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirConversationResource.GetConversationAsync(
            conversationId.ToString(), _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain(conversationId.ToString());
        result.ShouldContain("Hi there!");

        await _messageBus.Received(1)
            .InvokeAsync<ConversationDto>(
                Arg.Is<GetConversationByIdQuery>(q => q.ConversationId == conversationId),
                Arg.Any<CancellationToken>());
    }
}
