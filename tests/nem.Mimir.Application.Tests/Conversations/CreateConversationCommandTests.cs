using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class CreateConversationCommandTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreateConversationCommandHandler _handler;

    public CreateConversationCommandTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _mapper = new MimirMapper();

        _handler = new CreateConversationCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateConversationAndReturnDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.CreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Conversation>());

        var command = new CreateConversationCommand("Test Conversation", null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("Test Conversation");
        result.UserId.ShouldBe(userId);
        result.Status.ShouldBe("Active");

        await _repository.Received(1).CreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        var command = new CreateConversationCommand("Test", null, null);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyTitle_ShouldFail()
    {
        // Arrange
        var validator = new CreateConversationCommandValidator();
        var command = new CreateConversationCommand("", null, null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validator_TitleTooLong_ShouldFail()
    {
        // Arrange
        var validator = new CreateConversationCommandValidator();
        var command = new CreateConversationCommand(new string('x', 201), null, null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validator_ValidTitle_ShouldPass()
    {
        // Arrange
        var validator = new CreateConversationCommandValidator();
        var command = new CreateConversationCommand("Valid Title", null, null);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
