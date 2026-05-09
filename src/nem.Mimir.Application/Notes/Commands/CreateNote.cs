using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Notes.Commands;

public sealed record CreateNoteCommand(
    string Title,
    byte[] Content,
    IReadOnlyList<string> Tags) : ICommand<NoteDto>;

public sealed class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Note title is required.")
            .MaximumLength(200).WithMessage("Note title must not exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("Note content is required.");
    }
}

public sealed class CreateNoteHandler
{
    public async Task<NoteDto> Handle(
        CreateNoteCommand command,
        INoteRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        Common.Mappings.MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var note = Note.Create(userGuid, command.Title, command.Content, command.Tags);

        await repository.CreateAsync(note, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToNoteDto(note);
    }
}
