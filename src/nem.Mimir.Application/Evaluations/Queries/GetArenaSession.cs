using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Evaluations.Commands;

namespace nem.Mimir.Application.Evaluations.Queries;

public sealed record GetArenaSessionQuery(Guid SessionId) : IQuery<ArenaSessionDto>;

public sealed class GetArenaSessionQueryValidator : AbstractValidator<GetArenaSessionQuery>
{
    public GetArenaSessionQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required.");
    }
}

public sealed class GetArenaSessionHandler
{
    public async Task<ArenaSessionDto> Handle(
        GetArenaSessionQuery query,
        IEvaluationRepository evaluationRepository,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var session = await evaluationRepository.GetArenaSessionByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(nem.Mimir.Domain.Entities.ArenaSession), query.SessionId);

        if (session.UserId != userGuid)
            throw new ForbiddenAccessException();

        return new ArenaSessionDto(
            session.Id,
            session.Prompt,
            session.ResponseA,
            session.ResponseB,
            session.IsRevealed,
            session.IsRevealed ? session.ModelAName : null,
            session.IsRevealed ? session.ModelBName : null,
            session.VotedWinner,
            session.ModelAEloBefore,
            session.ModelAEloAfter,
            session.ModelBEloBefore,
            session.ModelBEloAfter);
    }
}
