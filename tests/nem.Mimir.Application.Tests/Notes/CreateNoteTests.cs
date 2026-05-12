using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Notes.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Notes;

public sealed class CreateNoteTests
{
    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateNoteAndReturnDto()
    {
        var repository = Substitute.For<INoteRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mapper = new MimirMapper();
        var handler = new CreateNoteHandler();

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId.ToString());

        repository.CreateAsync(Arg.Any<Note>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Note>());

        var command = new CreateNoteCommand("Sprint notes", [0x0A, 0x0B], ["sprint", "planning"]);

        var result = await handler.Handle(command, repository, currentUserService, unitOfWork, mapper, CancellationToken.None);

        result.Title.ShouldBe("Sprint notes");
        result.UserId.ShouldBe(userId);
        result.Content.ShouldNotBeNullOrWhiteSpace();
        result.Tags.ShouldContain("sprint");

        await repository.Received(1).CreateAsync(Arg.Any<Note>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ShouldThrowForbiddenAccessException()
    {
        var repository = Substitute.For<INoteRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mapper = new MimirMapper();
        var handler = new CreateNoteHandler();

        currentUserService.UserId.Returns((string?)null);

        var command = new CreateNoteCommand("Sprint notes", [0x0A], ["tag"]);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(command, repository, currentUserService, unitOfWork, mapper, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyTitle_ShouldFail()
    {
        var validator = new CreateNoteCommandValidator();
        var command = new CreateNoteCommand(string.Empty, [0x00], []);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == "Title");
    }
}
