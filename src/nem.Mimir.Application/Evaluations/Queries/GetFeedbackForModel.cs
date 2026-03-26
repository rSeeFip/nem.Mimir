using FluentValidation;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Evaluations.Queries;

public sealed record GetFeedbackForModelQuery(Guid ModelId, int PageNumber, int PageSize) : IQuery<PaginatedList<EvaluationFeedbackDto>>;

public sealed class GetFeedbackForModelQueryValidator : AbstractValidator<GetFeedbackForModelQuery>
{
    public GetFeedbackForModelQueryValidator()
    {
        RuleFor(x => x.ModelId)
            .NotEmpty().WithMessage("Model ID is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetFeedbackForModelHandler
{
    public async Task<PaginatedList<EvaluationFeedbackDto>> Handle(
        GetFeedbackForModelQuery query,
        IEvaluationRepository repository,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetFeedbackForModelAsync(query.ModelId, query.PageNumber, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToEvaluationFeedbackDto).ToList();
        return new PaginatedList<EvaluationFeedbackDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
