using Mimir.Domain.ValueObjects;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.SystemPrompts.Commands;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.SystemPrompts;

public sealed class DeleteSystemPromptCommandTests
{
    private readonly ISystemPromptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DeleteSystemPromptCommandHandler _handler;

    public DeleteSystemPromptCommandTests()
    {
        _repository = Substitute.For<ISystemPromptRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new DeleteSystemPromptCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ExistingPrompt_ShouldDelete()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Name", "Template", "Desc");
        var id = prompt.Id;

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(prompt);

        var command = new DeleteSystemPromptCommand(id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _repository.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentPrompt_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = SystemPromptId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((SystemPrompt?)null);

        var command = new DeleteSystemPromptCommand(id);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyId_ShouldFail()
    {
        var validator = new DeleteSystemPromptCommandValidator();
        var command = new DeleteSystemPromptCommand(SystemPromptId.Empty);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validator_ValidId_ShouldPass()
    {
        var validator = new DeleteSystemPromptCommandValidator();
        var command = new DeleteSystemPromptCommand(SystemPromptId.New());

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
