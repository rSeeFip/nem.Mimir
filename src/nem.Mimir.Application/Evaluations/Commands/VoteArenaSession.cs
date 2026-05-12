using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Services;

namespace nem.Mimir.Application.Evaluations.Commands;

public sealed record VoteArenaSessionCommand(Guid SessionId, string Winner) : ICommand<ArenaSessionDto>;

public sealed class VoteArenaSessionCommandValidator : AbstractValidator<VoteArenaSessionCommand>
{
    public VoteArenaSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required.");

        RuleFor(x => x.Winner)
            .NotEmpty().WithMessage("Winner is required.")
            .Must(winner => winner is "A" or "B" or "Draw")
            .WithMessage("Winner must be A, B, or Draw.");
    }
}

public sealed class VoteArenaSessionHandler
{
    public async Task<ArenaSessionDto> Handle(
        VoteArenaSessionCommand command,
        IEvaluationRepository evaluationRepository,
        IModelProfileRepository modelProfileRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var session = await evaluationRepository.GetArenaSessionByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ArenaSession), command.SessionId);

        if (session.UserId != userGuid)
            throw new ForbiddenAccessException();

        var profiles = await modelProfileRepository.GetByUserIdAsync(userGuid, cancellationToken).ConfigureAwait(false);
        var modelAProfile = profiles.FirstOrDefault(profile => profile.Id.Value == session.ModelAId)
            ?? throw new NotFoundException(nameof(ModelProfile), session.ModelAId);
        var modelBProfile = profiles.FirstOrDefault(profile => profile.Id.Value == session.ModelBId)
            ?? throw new NotFoundException(nameof(ModelProfile), session.ModelBId);

        var existingModelAEntry = await evaluationRepository.GetLeaderboardEntryByModelIdAsync(session.ModelAId, cancellationToken)
            .ConfigureAwait(false);
        var modelAEntry = existingModelAEntry ?? LeaderboardEntry.Create(session.ModelAId);

        var existingModelBEntry = await evaluationRepository.GetLeaderboardEntryByModelIdAsync(session.ModelBId, cancellationToken)
            .ConfigureAwait(false);
        var modelBEntry = existingModelBEntry ?? LeaderboardEntry.Create(session.ModelBId);

        var (scoreA, scoreB) = command.Winner switch
        {
            "A" => (1.0m, 0.0m),
            "B" => (0.0m, 1.0m),
            "Draw" => (0.5m, 0.5m),
            _ => throw new ArgumentOutOfRangeException(nameof(command.Winner), command.Winner, "Winner must be A, B, or Draw."),
        };

        var modelAEloBefore = modelAEntry.EloScore;
        var modelBEloBefore = modelBEntry.EloScore;

        var (newRatingA, newRatingB) = EloRatingService.CalculateMatchResult(
            modelAEloBefore,
            modelBEloBefore,
            scoreA,
            scoreB,
            modelAEntry.TotalEvaluations,
            modelBEntry.TotalEvaluations);

        modelAEntry.ApplyOutcome(scoreA, newRatingA);
        modelBEntry.ApplyOutcome(scoreB, newRatingB);

        session.Vote(
            command.Winner,
            modelAProfile.Name,
            modelBProfile.Name,
            modelAEloBefore,
            modelAEntry.EloScore,
            modelBEloBefore,
            modelBEntry.EloScore);

        await evaluationRepository.UpdateArenaSessionAsync(session, cancellationToken).ConfigureAwait(false);

        if (existingModelAEntry is null)
            await evaluationRepository.CreateLeaderboardEntryAsync(modelAEntry, cancellationToken).ConfigureAwait(false);
        else
            await evaluationRepository.UpdateLeaderboardEntryAsync(modelAEntry, cancellationToken).ConfigureAwait(false);

        if (existingModelBEntry is null)
            await evaluationRepository.CreateLeaderboardEntryAsync(modelBEntry, cancellationToken).ConfigureAwait(false);
        else
            await evaluationRepository.UpdateLeaderboardEntryAsync(modelBEntry, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ArenaSessionDto(
            session.Id,
            session.Prompt,
            session.ResponseA,
            session.ResponseB,
            session.IsRevealed,
            session.ModelAName,
            session.ModelBName,
            session.VotedWinner,
            session.ModelAEloBefore,
            session.ModelAEloAfter,
            session.ModelBEloBefore,
            session.ModelBEloAfter);
    }
}
