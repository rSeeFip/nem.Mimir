using FluentValidation;
using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Notes.Queries;

public sealed record GetNoteVersionsQuery(Guid NoteId, int PageNumber, int PageSize) : IQuery<PaginatedList<NoteVersionDto>>;

public sealed class GetNoteVersionsQueryValidator : AbstractValidator<GetNoteVersionsQuery>
{
    public GetNoteVersionsQueryValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetNoteVersionsHandler
{
    public async Task<PaginatedList<NoteVersionDto>> Handle(
        GetNoteVersionsQuery query,
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

        var result = await repository.GetVersionsAsync(
                note.Id,
                query.PageNumber,
                query.PageSize,
                cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToNoteVersionDto).ToList();
        return new PaginatedList<NoteVersionDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
