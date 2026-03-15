using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Users.Commands;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Users;

public sealed class DeactivateUserCommandTests
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DeactivateUserCommandHandler _handler;

    public DeactivateUserCommandTests()
    {
        _repository = Substitute.For<IUserRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new DeactivateUserCommandHandler(_repository, _currentUserService, _unitOfWork);
    }

    [Fact]
    public async Task Handle_AdminDeactivatesUser_ShouldDeactivateAndSave()
    {
        // Arrange
        var user = User.Create("testuser", "test@example.com", UserRole.User);

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.IsActive.ShouldBeFalse();
        await _repository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonAdmin_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "User" });

        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Unauthenticated_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(false);
        _currentUserService.Roles.Returns(new List<string>());

        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyUserId_ShouldFail()
    {
        // Arrange
        var validator = new DeactivateUserCommandValidator();
        var command = new DeactivateUserCommand(Guid.Empty);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        // Arrange
        var validator = new DeactivateUserCommandValidator();
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
