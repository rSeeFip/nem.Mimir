using FluentValidation;
using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Notes.Commands;

public sealed record RestoreNoteVersionCommand(Guid NoteId, Guid VersionId) : ICommand;

public sealed class RestoreNoteVersionCommandValidator : AbstractValidator<RestoreNoteVersionCommand>
{
    public RestoreNoteVersionCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");

        RuleFor(x => x.VersionId)
            .NotEmpty().WithMessage("Version ID is required.");
    }
}

public sealed class RestoreNoteVersionHandler
{
    public async Task Handle(
        RestoreNoteVersionCommand command,
        INoteRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var note = await repository.GetWithVersionsAsync(NoteTypedId.From(command.NoteId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Note), command.NoteId);

        if (!note.CanEdit(userGuid))
            throw new ForbiddenAccessException();

        try
        {
            note.RestoreVersion(userGuid, command.VersionId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Version not found.", StringComparison.Ordinal))
        {
            throw new NotFoundException("NoteVersion", command.VersionId);
        }

        await repository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
