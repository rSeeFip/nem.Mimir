using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Evaluations.Queries;

public sealed record GetEvaluationHistoryQuery(int PageNumber, int PageSize) : IQuery<PaginatedList<EvaluationDto>>;

public sealed class GetEvaluationHistoryQueryValidator : AbstractValidator<GetEvaluationHistoryQuery>
{
    public GetEvaluationHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetEvaluationHistoryHandler
{
    public async Task<PaginatedList<EvaluationDto>> Handle(
        GetEvaluationHistoryQuery query,
        IEvaluationRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var result = await repository.GetEvaluationHistoryAsync(userGuid, query.PageNumber, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToEvaluationDto).ToList();
        return new PaginatedList<EvaluationDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
