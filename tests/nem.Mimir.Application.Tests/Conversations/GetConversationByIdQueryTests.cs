using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class GetConversationByIdQueryTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;
    private readonly GetConversationByIdQueryHandler _handler;

    public GetConversationByIdQueryTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _mapper = new MimirMapper();

        _handler = new GetConversationByIdQueryHandler(_repository, _currentUserService, _mapper);
    }

    [Fact]
    public async Task Handle_ValidQuery_ShouldReturnConversationDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test Conversation");
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var query = new GetConversationByIdQuery(conversation.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("Test Conversation");
        result.UserId.ShouldBe(userId);
        result.Status.ShouldBe("Active");
        result.Messages.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());

        _repository.GetWithMessagesAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var query = new GetConversationByIdQuery(conversationId);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongUser_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var conversation = Conversation.Create(ownerId, "Title");
        _currentUserService.UserId.Returns(otherUserId.ToString());

        _repository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var query = new GetConversationByIdQuery(conversation.Id);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
