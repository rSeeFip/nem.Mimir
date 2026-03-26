using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Notes.Queries;

public sealed record GetNotesQuery(int PageNumber, int PageSize) : IQuery<PaginatedList<NoteListDto>>;

public sealed class GetNotesQueryValidator : AbstractValidator<GetNotesQuery>
{
    public GetNotesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetNotesHandler
{
    public async Task<PaginatedList<NoteListDto>> Handle(
        GetNotesQuery query,
        INoteRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var result = await repository.GetByUserIdAsync(userGuid, query.PageNumber, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToNoteListDto).ToList();
        return new PaginatedList<NoteListDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
