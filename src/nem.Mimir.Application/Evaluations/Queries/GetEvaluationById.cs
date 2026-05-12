using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;

namespace nem.Mimir.Application.Evaluations.Queries;

public sealed record GetEvaluationByIdQuery(Guid EvaluationId) : IQuery<EvaluationDto>;

public sealed class GetEvaluationByIdQueryValidator : AbstractValidator<GetEvaluationByIdQuery>
{
    public GetEvaluationByIdQueryValidator()
    {
        RuleFor(x => x.EvaluationId)
            .NotEmpty().WithMessage("Evaluation ID is required.");
    }
}

public sealed class GetEvaluationByIdHandler
{
    public async Task<EvaluationDto> Handle(
        GetEvaluationByIdQuery query,
        IEvaluationRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var evaluation = await repository.GetByIdAsync(EvaluationId.From(query.EvaluationId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Evaluation), query.EvaluationId);

        if (evaluation.UserId != userGuid)
            throw new ForbiddenAccessException();

        return mapper.MapToEvaluationDto(evaluation);
    }
}
