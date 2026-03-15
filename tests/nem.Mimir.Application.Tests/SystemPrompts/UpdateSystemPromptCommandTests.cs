using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.SystemPrompts.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.SystemPrompts;

public sealed class UpdateSystemPromptCommandTests
{
    private readonly ISystemPromptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateSystemPromptCommandHandler _handler;

    public UpdateSystemPromptCommandTests()
    {
        _repository = Substitute.For<ISystemPromptRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new UpdateSystemPromptCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ExistingPrompt_ShouldUpdateFields()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Old Name", "Old Template", "Old Desc");
        var id = prompt.Id;

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(prompt);

        var command = new UpdateSystemPromptCommand(id, "New Name", "New Template", "New Desc");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        prompt.Name.ShouldBe("New Name");
        prompt.Template.ShouldBe("New Template");
        prompt.Description.ShouldBe("New Desc");

        await _repository.Received(1).UpdateAsync(prompt, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentPrompt_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((SystemPrompt?)null);

        var command = new UpdateSystemPromptCommand(id, "Name", "Template", "Desc");

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyId_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.Empty, "Name", "Template", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validator_EmptyName_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "", "Template", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "Name", "Template", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
