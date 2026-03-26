using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class AttachFileToConversationCommandTests
{
    [Fact]
    public async Task Handle_WithOwnedConversation_ShouldUploadIngestAndAssociateCollection()
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

        knowledgeCollectionRepository.GetByUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<KnowledgeCollection>());

        var uploadedDocId = Guid.NewGuid();
        mediaHubClient.UploadAsync("doc.txt", "text/plain", Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new MediaHubUploadResult(uploadedDocId, "https://media/doc", "text/plain", 123));

        var handler = new AttachFileToConversationCommandHandler(
            conversationRepository,
            knowledgeCollectionRepository,
            mediaHubClient,
            knowledgeIngestionService,
            currentUserService,
            unitOfWork);

        var result = await handler.Handle(
            new AttachFileToConversationCommand(conversation.Id, "doc.txt", "text/plain", [1, 2]),
            CancellationToken.None);

        result.DocumentId.ShouldBe(uploadedDocId);
        result.FileName.ShouldBe("doc.txt");
        result.ProcessingStatus.ShouldBe("Ready");

        await knowledgeCollectionRepository.Received(1).CreateAsync(Arg.Any<KnowledgeCollection>(), Arg.Any<CancellationToken>());
        await knowledgeCollectionRepository.Received(1).UpdateAsync(Arg.Any<KnowledgeCollection>(), Arg.Any<CancellationToken>());
        await knowledgeIngestionService.Received(1).IngestDocumentAsync(uploadedDocId, "doc.txt", "https://media/doc", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnownedConversation_ShouldThrowForbidden()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var knowledgeCollectionRepository = Substitute.For<IKnowledgeCollectionRepository>();
        var mediaHubClient = Substitute.For<IMediaHubClient>();
        var knowledgeIngestionService = Substitute.For<IKnowledgeIngestionService>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        var conversation = Conversation.Create(Guid.NewGuid(), "Other chat");
        conversationRepository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var handler = new AttachFileToConversationCommandHandler(
            conversationRepository,
            knowledgeCollectionRepository,
            mediaHubClient,
            knowledgeIngestionService,
            currentUserService,
            unitOfWork);

        await Should.ThrowAsync<ForbiddenAccessException>(() => handler.Handle(
            new AttachFileToConversationCommand(conversation.Id, "doc.txt", "text/plain", [1]),
            CancellationToken.None));
    }
}
