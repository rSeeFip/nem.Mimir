using FluentValidation;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Evaluations.Queries;

public sealed record GetLeaderboardQuery(int PageNumber, int PageSize) : IQuery<PaginatedList<LeaderboardEntryDto>>;

public sealed class GetLeaderboardQueryValidator : AbstractValidator<GetLeaderboardQuery>
{
    public GetLeaderboardQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetLeaderboardHandler
{
    public async Task<PaginatedList<LeaderboardEntryDto>> Handle(
        GetLeaderboardQuery query,
        IEvaluationRepository repository,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetLeaderboardAsync(query.PageNumber, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToLeaderboardEntryDto).ToList();
        return new PaginatedList<LeaderboardEntryDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
