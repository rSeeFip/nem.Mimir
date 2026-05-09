using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Prompts.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Prompts;

public sealed class UpdatePromptTemplateTests
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdatePromptTemplateCommandHandler _handler;

    public UpdatePromptTemplateTests()
    {
        _repository = Substitute.For<IPromptTemplateRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new UpdatePromptTemplateCommandHandler(_repository, _currentUserService, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldUpdateAndAppendVersion()
    {
        var userId = Guid.NewGuid();
        var template = PromptTemplate.Create(userId, "Old", "/old", "v1", ["one"]);

        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByIdForUserAsync(template.Id, userId, Arg.Any<CancellationToken>())
            .Returns(template);
        _repository.GetByCommandForUserAsync("/new", userId, Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null);

        var command = new UpdatePromptTemplateCommand(template.Id, "New", "/new", "v2", ["two"]);

        await _handler.Handle(command, CancellationToken.None);

        template.Title.ShouldBe("New");
        template.Command.ShouldBe("/new");
        template.Content.ShouldBe("v2");
        template.Tags.ShouldContain("two");
        template.VersionHistory.Count.ShouldBe(2);

        await _repository.Received(1).UpdateAsync(template, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateCommandOnAnotherTemplate_ShouldThrowConflict()
    {
        var userId = Guid.NewGuid();
        var template = PromptTemplate.Create(userId, "A", "/a", "content-a");
        var anotherTemplate = PromptTemplate.Create(userId, "B", "/b", "content-b");

        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByIdForUserAsync(template.Id, userId, Arg.Any<CancellationToken>())
            .Returns(template);
        _repository.GetByCommandForUserAsync("/b", userId, Arg.Any<CancellationToken>())
            .Returns(anotherTemplate);

        var command = new UpdatePromptTemplateCommand(template.Id, "A2", "/b", "new", null);

        await Should.ThrowAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyId_ShouldFail()
    {
        var validator = new UpdatePromptTemplateCommandValidator();
        var command = new UpdatePromptTemplateCommand(PromptTemplateId.Empty, "Name", "/cmd", "content", null);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }
}
