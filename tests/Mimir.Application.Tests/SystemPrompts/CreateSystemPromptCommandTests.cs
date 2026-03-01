using AutoMapper;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.SystemPrompts.Commands;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.SystemPrompts;

public sealed class CreateSystemPromptCommandTests
{
    private readonly ISystemPromptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly CreateSystemPromptCommandHandler _handler;

    public CreateSystemPromptCommandTests()
    {
        _repository = Substitute.For<ISystemPromptRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _handler = new CreateSystemPromptCommandHandler(_repository, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateAndReturnDto()
    {
        // Arrange
        _repository.CreateAsync(Arg.Any<SystemPrompt>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<SystemPrompt>());

        var command = new CreateSystemPromptCommand("Test Prompt", "Hello {{name}}", "A test prompt");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test Prompt");
        result.Template.ShouldBe("Hello {{name}}");
        result.Description.ShouldBe("A test prompt");
        result.IsDefault.ShouldBeFalse();
        result.IsActive.ShouldBeTrue();
        result.Id.ShouldNotBe(Guid.Empty);

        await _repository.Received(1).CreateAsync(Arg.Any<SystemPrompt>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyName_ShouldFail()
    {
        var validator = new CreateSystemPromptCommandValidator();
        var command = new CreateSystemPromptCommand("", "template", "desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validator_EmptyTemplate_ShouldFail()
    {
        var validator = new CreateSystemPromptCommandValidator();
        var command = new CreateSystemPromptCommand("Name", "", "desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Template");
    }

    [Fact]
    public void Validator_NameTooLong_ShouldFail()
    {
        var validator = new CreateSystemPromptCommandValidator();
        var command = new CreateSystemPromptCommand(new string('x', 201), "template", "desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validator_TemplateTooLong_ShouldFail()
    {
        var validator = new CreateSystemPromptCommandValidator();
        var command = new CreateSystemPromptCommand("Name", new string('x', 10001), "desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Template");
    }

    [Fact]
    public void Validator_DescriptionTooLong_ShouldFail()
    {
        var validator = new CreateSystemPromptCommandValidator();
        var command = new CreateSystemPromptCommand("Name", "template", new string('x', 1001));

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        var validator = new CreateSystemPromptCommandValidator();
        var command = new CreateSystemPromptCommand("Valid Name", "Valid template", "Valid desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
