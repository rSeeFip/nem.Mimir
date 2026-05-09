using FluentValidation;
using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Notes.Commands;

public sealed record UpdateNoteCommand(
    Guid NoteId,
    string Title,
    byte[] Content,
    string? ChangeDescription,
    IReadOnlyList<string> Tags) : ICommand;

public sealed class UpdateNoteCommandValidator : AbstractValidator<UpdateNoteCommand>
{
    public UpdateNoteCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Note title is required.")
            .MaximumLength(200).WithMessage("Note title must not exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("Note content is required.");
    }
}

public sealed class UpdateNoteHandler
{
    public async Task Handle(
        UpdateNoteCommand command,
        INoteRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var note = await repository.GetWithCollaboratorsAsync(NoteTypedId.From(command.NoteId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Note), command.NoteId);

        note.Update(userGuid, command.Title, command.Content, command.ChangeDescription);

        foreach (var existing in note.Tags.ToList())
        {
            note.RemoveTag(existing);
        }

        foreach (var tag in command.Tags)
        {
            note.AddTag(tag);
        }

        await repository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
