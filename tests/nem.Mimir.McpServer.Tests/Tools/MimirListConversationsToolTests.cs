using NSubstitute;
using Shouldly;
using Wolverine;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirListConversationsToolTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task ListConversationsAsync_ReturnsSerializedPaginatedList()
    {
        var items = new List<ConversationListDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), null, false, null, [], "Conv1", "Active", 5, DateTimeOffset.UtcNow, null)
        };
        var expected = new PaginatedList<ConversationListDto>(items, 1, 1, 1);

        _messageBus
            .InvokeAsync<PaginatedList<ConversationListDto>>(
                Arg.Any<GetConversationsByUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirListConversationsTool.ListConversationsAsync(
            1, 20, _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("Conv1");
    }

    [Fact]
    public async Task ListConversationsAsync_ClampsBadPageParams()
    {
        var expected = new PaginatedList<ConversationListDto>([], 1, 0, 0);

        _messageBus
            .InvokeAsync<PaginatedList<ConversationListDto>>(
                Arg.Any<GetConversationsByUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        await MimirListConversationsTool.ListConversationsAsync(
            -1, 0, _messageBus, CancellationToken.None);

        await _messageBus.Received(1)
            .InvokeAsync<PaginatedList<ConversationListDto>>(
                Arg.Is<GetConversationsByUserQuery>(q =>
                    q.PageNumber == 1 && q.PageSize == 20),
                Arg.Any<CancellationToken>());
    }
}
