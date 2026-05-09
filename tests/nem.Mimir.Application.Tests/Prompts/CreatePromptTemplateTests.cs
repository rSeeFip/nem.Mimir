using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Prompts.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Prompts;

public sealed class CreatePromptTemplateTests
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreatePromptTemplateCommandHandler _handler;

    public CreatePromptTemplateTests()
    {
        _repository = Substitute.For<IPromptTemplateRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _mapper = new MimirMapper();

        _handler = new CreatePromptTemplateCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldCreateTemplateAndReturnDto()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetByCommandForUserAsync("/summarize", userId, Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null);
        _repository.CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<PromptTemplate>());

        var command = new CreatePromptTemplateCommand(
            "Summarize",
            "/summarize",
            "Summarize {{topic}}",
            ["Writing"],
            false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.Title.ShouldBe("Summarize");
        result.Command.ShouldBe("/summarize");
        result.Content.ShouldBe("Summarize {{topic}}");
        result.IsShared.ShouldBeFalse();
        result.Tags.ShouldContain("writing");
        result.VersionHistory.Count.ShouldBe(1);
        result.UsageCount.ShouldBe(0);

        await _repository.Received(1).CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateCommand_ShouldThrowConflict()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetByCommandForUserAsync("/summarize", userId, Arg.Any<CancellationToken>())
            .Returns(PromptTemplate.Create(userId, "Existing", "/summarize", "existing"));

        var command = new CreatePromptTemplateCommand("New", "/summarize", "content", null, false);

        await Should.ThrowAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_InvalidCommand_ShouldFail()
    {
        var validator = new CreatePromptTemplateCommandValidator();
        var command = new CreatePromptTemplateCommand("Name", "invalid", "content", null, false);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Command");
    }
}
