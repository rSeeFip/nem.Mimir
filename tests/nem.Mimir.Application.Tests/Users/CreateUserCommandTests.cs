using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Users.Commands;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Users;

public sealed class CreateUserCommandTests
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandTests()
    {
        _repository = Substitute.For<IUserRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _mapper = new MimirMapper();

        _handler = new CreateUserCommandHandler(_repository, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateUserAndReturnDto()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        var command = new CreateUserCommand("test@example.com", "Test User");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Username.ShouldBe("Test User");
        result.Email.ShouldBe("test@example.com");
        result.Role.ShouldBe("User");
        result.IsActive.ShouldBeTrue();

        await _repository.Received(1).CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAdminRole_ShouldCreateAdminUser()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        var command = new CreateUserCommand("admin@example.com", "Admin User", UserRole.Admin);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Role.ShouldBe("Admin");
    }

    [Fact]
    public void Validator_EmptyEmail_ShouldFail()
    {
        // Arrange
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("", "Test User");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validator_InvalidEmail_ShouldFail()
    {
        // Arrange
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("not-an-email", "Test User");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validator_EmptyDisplayName_ShouldFail()
    {
        // Arrange
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("test@example.com", "");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void Validator_DisplayNameTooLong_ShouldFail()
    {
        // Arrange
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("test@example.com", new string('x', 101));

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        // Arrange
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("test@example.com", "Test User");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
