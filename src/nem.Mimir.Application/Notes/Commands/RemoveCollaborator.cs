using FluentValidation;
using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Notes.Commands;

public sealed record RemoveCollaboratorCommand(Guid NoteId, Guid UserId) : ICommand;

public sealed class RemoveCollaboratorCommandValidator : AbstractValidator<RemoveCollaboratorCommand>
{
    public RemoveCollaboratorCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}

public sealed class RemoveCollaboratorHandler
{
    public async Task Handle(
        RemoveCollaboratorCommand command,
        INoteRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(actor, out var actorGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var note = await repository.GetWithCollaboratorsAsync(NoteTypedId.From(command.NoteId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Note), command.NoteId);

        if (note.OwnerId != actorGuid)
            throw new ForbiddenAccessException();

        note.RemoveCollaborator(command.UserId);

        await repository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
