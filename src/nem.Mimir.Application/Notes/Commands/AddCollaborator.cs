using FluentValidation;
using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Application.Notes.Commands;

public sealed record AddCollaboratorCommand(Guid NoteId, Guid UserId, string Permission) : ICommand;

public sealed class AddCollaboratorCommandValidator : AbstractValidator<AddCollaboratorCommand>
{
    public AddCollaboratorCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Permission)
            .NotEmpty().WithMessage("Permission is required.")
            .Must(value => Enum.TryParse<NotePermission>(value, true, out _))
            .WithMessage("Permission must be Owner, Editor, or Viewer.");
    }
}

public sealed class AddCollaboratorHandler
{
    public async Task Handle(
        AddCollaboratorCommand command,
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

        var permission = Enum.Parse<NotePermission>(command.Permission, true);
        note.AddCollaborator(command.UserId, permission);

        await repository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
