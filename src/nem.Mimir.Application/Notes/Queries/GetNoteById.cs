using FluentValidation;
using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Notes.Queries;

public sealed record GetNoteByIdQuery(Guid NoteId) : IQuery<NoteDto>;

public sealed class GetNoteByIdQueryValidator : AbstractValidator<GetNoteByIdQuery>
{
    public GetNoteByIdQueryValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");
    }
}

public sealed class GetNoteByIdHandler
{
    public async Task<NoteDto> Handle(
        GetNoteByIdQuery query,
        INoteRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var note = await repository.GetWithCollaboratorsAsync(NoteTypedId.From(query.NoteId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Note), query.NoteId);

        if (!note.CanView(userGuid))
            throw new ForbiddenAccessException();

        return mapper.MapToNoteDto(note);
    }
}
