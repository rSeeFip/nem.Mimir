using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Services;
using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;

namespace nem.Mimir.Application.Evaluations.Commands;

public sealed record SubmitEvaluationResultCommand(Guid EvaluationId, string Winner) : ICommand<EvaluationDto>;

public sealed class SubmitEvaluationResultCommandValidator : AbstractValidator<SubmitEvaluationResultCommand>
{
    public SubmitEvaluationResultCommandValidator()
    {
        RuleFor(x => x.EvaluationId)
            .NotEmpty().WithMessage("Evaluation ID is required.");

        RuleFor(x => x.Winner)
            .NotEmpty().WithMessage("Winner is required.")
            .Must(value => Enum.TryParse<EvaluationWinner>(value, true, out var parsed) && parsed != EvaluationWinner.None)
            .WithMessage("Winner must be ModelA, ModelB, or Draw.");
    }
}

public sealed class SubmitEvaluationResultHandler
{
    public async Task<EvaluationDto> Handle(
        SubmitEvaluationResultCommand command,
        IEvaluationRepository repository,
        IUnitOfWork unitOfWork,
        Common.Mappings.MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var evaluation = await repository.GetByIdAsync(EvaluationId.From(command.EvaluationId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Evaluation), command.EvaluationId);

        var winner = Enum.Parse<EvaluationWinner>(command.Winner, true);
        evaluation.SubmitResult(winner);

        var existingModelAEntry = await repository.GetLeaderboardEntryByModelIdAsync(evaluation.ModelAId, cancellationToken)
            .ConfigureAwait(false);
        var modelAEntry = existingModelAEntry ?? LeaderboardEntry.Create(evaluation.ModelAId);

        var existingModelBEntry = await repository.GetLeaderboardEntryByModelIdAsync(evaluation.ModelBId, cancellationToken)
            .ConfigureAwait(false);
        var modelBEntry = existingModelBEntry ?? LeaderboardEntry.Create(evaluation.ModelBId);

        var (scoreA, scoreB) = winner switch
        {
            EvaluationWinner.ModelA => (1.0m, 0.0m),
            EvaluationWinner.ModelB => (0.0m, 1.0m),
            EvaluationWinner.Draw => (0.5m, 0.5m),
            _ => throw new InvalidOperationException("Unsupported winner value."),
        };

        var (newRatingA, newRatingB) = EloRatingService.CalculateMatchResult(
            modelAEntry.EloScore,
            modelBEntry.EloScore,
            scoreA,
            scoreB,
            modelAEntry.TotalEvaluations,
            modelBEntry.TotalEvaluations);

        modelAEntry.ApplyOutcome(scoreA, newRatingA);
        modelBEntry.ApplyOutcome(scoreB, newRatingB);

        await repository.UpdateEvaluationAsync(evaluation, cancellationToken).ConfigureAwait(false);

        if (existingModelAEntry is null)
            await repository.CreateLeaderboardEntryAsync(modelAEntry, cancellationToken).ConfigureAwait(false);
        else
            await repository.UpdateLeaderboardEntryAsync(modelAEntry, cancellationToken).ConfigureAwait(false);

        if (existingModelBEntry is null)
            await repository.CreateLeaderboardEntryAsync(modelBEntry, cancellationToken).ConfigureAwait(false);
        else
            await repository.UpdateLeaderboardEntryAsync(modelBEntry, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToEvaluationDto(evaluation);
    }
}
