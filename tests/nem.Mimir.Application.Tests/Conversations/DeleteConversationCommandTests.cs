using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class DeleteConversationCommandTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DeleteConversationCommandHandler _handler;

    public DeleteConversationCommandTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new DeleteConversationCommandHandler(_repository, _currentUserService, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldDeleteConversation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Title");
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new DeleteConversationCommand(conversation.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _repository.Received(1).DeleteAsync(conversation.Id, Arg.Any<CancellationToken>());
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

        var command = new DeleteConversationCommand(conversationId);

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

        var command = new DeleteConversationCommand(conversation.Id);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        // Arrange
        var validator = new DeleteConversationCommandValidator();
        var command = new DeleteConversationCommand(Guid.Empty);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_ValidConversationId_ShouldPass()
    {
        // Arrange
        var validator = new DeleteConversationCommandValidator();
        var command = new DeleteConversationCommand(Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
