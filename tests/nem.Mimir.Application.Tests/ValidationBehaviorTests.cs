using FluentValidation;
using FluentValidation.Results;
using MediatR;
using nem.Mimir.Application.Common.Behaviours;
using NSubstitute;
using Shouldly;
using ValidationException = nem.Mimir.Application.Common.Exceptions.ValidationException;

namespace nem.Mimir.Application.Tests;

public sealed record TestValidationRequest(string Name) : IRequest<string>;

public sealed class ValidationBehaviorTests
{

    [Fact]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestValidationRequest>>();
        var behavior = new ValidationBehavior<TestValidationRequest, string>(validators);
        var request = new TestValidationRequest("test");

        RequestHandlerDelegate<string> next = () => Task.FromResult("success");

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.ShouldBe("success");
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_ShouldCallNext()
    {
        // Arrange
        var validator = Substitute.For<IValidator<TestValidationRequest>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<TestValidationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var validators = new[] { validator };
        var behavior = new ValidationBehavior<TestValidationRequest, string>(validators);
        var request = new TestValidationRequest("valid-name");

        var nextCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("success");
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.ShouldBe("success");
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldThrowValidationException()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
            new("Name", "Name must be at least 3 characters"),
        };

        var validator = Substitute.For<IValidator<TestValidationRequest>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<TestValidationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var validators = new[] { validator };
        var behavior = new ValidationBehavior<TestValidationRequest, string>(validators);
        var request = new TestValidationRequest("");

        RequestHandlerDelegate<string> next = () => Task.FromResult("should-not-reach");

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => behavior.Handle(request, next, CancellationToken.None));

        exception.Errors.ShouldContainKey("Name");
        exception.Errors["Name"].Length.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldNotCallNext()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
        };

        var validator = Substitute.For<IValidator<TestValidationRequest>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<TestValidationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var validators = new[] { validator };
        var behavior = new ValidationBehavior<TestValidationRequest, string>(validators);
        var request = new TestValidationRequest("");

        var nextCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("should-not-reach");
        };

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(
            () => behavior.Handle(request, next, CancellationToken.None));

        nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_WhenMultipleValidatorsFail_ShouldCollectAllErrors()
    {
        // Arrange
        var validator1 = Substitute.For<IValidator<TestValidationRequest>>();
        validator1
            .ValidateAsync(Arg.Any<ValidationContext<TestValidationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Name", "Error from validator 1") }));

        var validator2 = Substitute.For<IValidator<TestValidationRequest>>();
        validator2
            .ValidateAsync(Arg.Any<ValidationContext<TestValidationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Name", "Error from validator 2") }));

        var validators = new[] { validator1, validator2 };
        var behavior = new ValidationBehavior<TestValidationRequest, string>(validators);
        var request = new TestValidationRequest("");

        RequestHandlerDelegate<string> next = () => Task.FromResult("should-not-reach");

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => behavior.Handle(request, next, CancellationToken.None));

        exception.Errors["Name"].Length.ShouldBe(2);
        exception.Errors["Name"].ShouldContain("Error from validator 1");
        exception.Errors["Name"].ShouldContain("Error from validator 2");
    }
}
