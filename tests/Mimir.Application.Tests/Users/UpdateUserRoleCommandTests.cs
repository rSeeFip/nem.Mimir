using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.Users.Commands;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Users;

public sealed class UpdateUserRoleCommandTests
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly UpdateUserRoleCommandHandler _handler;

    public UpdateUserRoleCommandTests()
    {
        _repository = Substitute.For<IUserRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _mapper = new MimirMapper();

        _handler = new UpdateUserRoleCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_AdminChangesRole_ShouldUpdateAndReturnDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("testuser", "test@example.com", UserRole.User);

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new UpdateUserRoleCommand(userId, UserRole.Admin);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Role.ShouldBe("Admin");

        await _repository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonAdmin_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "User" });

        var command = new UpdateUserRoleCommand(Guid.NewGuid(), UserRole.Admin);

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

        var command = new UpdateUserRoleCommand(Guid.NewGuid(), UserRole.Admin);

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

        var command = new UpdateUserRoleCommand(Guid.NewGuid(), UserRole.Admin);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyUserId_ShouldFail()
    {
        // Arrange
        var validator = new UpdateUserRoleCommandValidator();
        var command = new UpdateUserRoleCommand(Guid.Empty, UserRole.Admin);

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
        var validator = new UpdateUserRoleCommandValidator();
        var command = new UpdateUserRoleCommand(Guid.NewGuid(), UserRole.Admin);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
