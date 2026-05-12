using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Application.Knowledge.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Knowledge;

public sealed class UploadChatFileTests
{
    [Fact]
    public async Task Handle_WhenConversationOwned_ShouldUploadAndIndex()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var knowledgeCollectionRepository = Substitute.For<IKnowledgeCollectionRepository>();
        var mediaHubClient = Substitute.For<IMediaHubClient>();
        var knowledgeIngestionService = Substitute.For<IKnowledgeIngestionService>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "Chat");
        conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var uploadId = Guid.NewGuid();
        mediaHubClient.UploadAsync("a.txt", "text/plain", Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new MediaHubUploadResult(uploadId, "https://media/file", "text/plain", 12));

        var handler = new UploadChatFileCommandHandler(
            conversationRepository,
            knowledgeCollectionRepository,
            mediaHubClient,
            knowledgeIngestionService,
            currentUserService,
            unitOfWork);

        var result = await handler.Handle(
            new UploadChatFileCommand(conversation.Id, "a.txt", "text/plain", [1, 2, 3], null),
            CancellationToken.None);

        result.DocumentId.ShouldBe(uploadId);
        result.IndexingStatus.ShouldBe("Indexed");
        await knowledgeIngestionService.Received(1).IngestDocumentAsync(uploadId, "a.txt", "https://media/file", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConversationNotOwned_ShouldThrowForbidden()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var knowledgeCollectionRepository = Substitute.For<IKnowledgeCollectionRepository>();
        var mediaHubClient = Substitute.For<IMediaHubClient>();
        var knowledgeIngestionService = Substitute.For<IKnowledgeIngestionService>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        var conversation = Conversation.Create(Guid.NewGuid(), "Other user's chat");
        conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var handler = new UploadChatFileCommandHandler(
            conversationRepository,
            knowledgeCollectionRepository,
            mediaHubClient,
            knowledgeIngestionService,
            currentUserService,
            unitOfWork);

        await Should.ThrowAsync<ForbiddenAccessException>(() => handler.Handle(
            new UploadChatFileCommand(conversation.Id, "a.txt", "text/plain", [1], null),
            CancellationToken.None));
    }
}
