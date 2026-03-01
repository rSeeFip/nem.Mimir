using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.Conversations.Commands;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Conversations;

public sealed class UpdateConversationTitleCommandTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly UpdateConversationTitleCommandHandler _handler;

    public UpdateConversationTitleCommandTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _mapper = new MimirMapper();

        _handler = new UpdateConversationTitleCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldUpdateTitleAndReturnDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Old Title");
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new UpdateConversationTitleCommand(conversation.Id, "New Title");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("New Title");

        await _repository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());

        _repository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var command = new UpdateConversationTitleCommand(conversationId, "New Title");

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
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

        var command = new UpdateConversationTitleCommand(conversation.Id, "New Title");

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        // Arrange
        var validator = new UpdateConversationTitleCommandValidator();
        var command = new UpdateConversationTitleCommand(Guid.Empty, "New Title");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_EmptyNewTitle_ShouldFail()
    {
        // Arrange
        var validator = new UpdateConversationTitleCommandValidator();
        var command = new UpdateConversationTitleCommand(Guid.NewGuid(), "");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "NewTitle");
    }

    [Fact]
    public void Validator_TitleTooLong_ShouldFail()
    {
        // Arrange
        var validator = new UpdateConversationTitleCommandValidator();
        var command = new UpdateConversationTitleCommand(Guid.NewGuid(), new string('x', 201));

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "NewTitle");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        // Arrange
        var validator = new UpdateConversationTitleCommandValidator();
        var command = new UpdateConversationTitleCommand(Guid.NewGuid(), "Valid Title");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
