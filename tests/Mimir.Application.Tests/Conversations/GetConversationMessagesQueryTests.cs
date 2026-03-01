using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.Conversations.Queries;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Conversations;

public sealed class GetConversationMessagesQueryTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;
    private readonly GetConversationMessagesQueryHandler _handler;

    public GetConversationMessagesQueryTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _mapper = new MimirMapper();

        _handler = new GetConversationMessagesQueryHandler(_repository, _currentUserService, _mapper);
    }

    [Fact]
    public async Task Handle_ValidQuery_ShouldReturnPaginatedMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Test Conversation");
        var message = conversation.AddMessage(MessageRole.User, "Hello");

        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var messages = new List<Message> { message };
        var paginatedMessages = new PaginatedList<Message>(
            messages.AsReadOnly(), 1, 1, 1);

        _repository.GetMessagesAsync(conversation.Id, 1, 20, Arg.Any<CancellationToken>())
            .Returns(paginatedMessages);

        var query = new GetConversationMessagesQuery(conversation.Id, 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.Items.First().Content.ShouldBe("Hello");
        result.Items.First().Role.ShouldBe("User");
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());

        _repository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var query = new GetConversationMessagesQuery(conversationId, 1, 20);

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

        _repository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var query = new GetConversationMessagesQuery(conversation.Id, 1, 20);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationMessagesQueryValidator();
        var query = new GetConversationMessagesQuery(Guid.Empty, 1, 20);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_PageNumberZero_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationMessagesQueryValidator();
        var query = new GetConversationMessagesQuery(Guid.NewGuid(), 0, 20);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void Validator_PageSizeTooLarge_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationMessagesQueryValidator();
        var query = new GetConversationMessagesQuery(Guid.NewGuid(), 1, 101);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_PageSizeZero_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationMessagesQueryValidator();
        var query = new GetConversationMessagesQuery(Guid.NewGuid(), 1, 0);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_ValidQuery_ShouldPass()
    {
        // Arrange
        var validator = new GetConversationMessagesQueryValidator();
        var query = new GetConversationMessagesQuery(Guid.NewGuid(), 1, 50);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
