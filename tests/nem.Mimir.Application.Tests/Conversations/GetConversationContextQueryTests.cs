using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using nem.Mimir.Application.Conversations.Services;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class GetConversationContextQueryTests
{
    [Fact]
    public async Task Handle_ShouldReturnContextFromRagService()
    {
        var ragService = Substitute.For<IConversationContextService>();
        var conversationId = Guid.NewGuid();
        ragService.GetRagContextAsync(conversationId, "What is in file?", Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeSearchResultDto>
            {
                new(Guid.NewGuid(), "chunk", 0.9f, "Document", "d1"),
            });

        var handler = new GetConversationContextQueryHandler(ragService);

        var result = await handler.Handle(
            new GetConversationContextQuery(conversationId, "What is in file?", 10),
            CancellationToken.None);

        result.ConversationId.ShouldBe(conversationId);
        result.Query.ShouldBe("What is in file?");
        result.KnowledgeSources.Count.ShouldBe(1);
        result.KnowledgeSources[0].ChunkText.ShouldBe("chunk");
    }
}
