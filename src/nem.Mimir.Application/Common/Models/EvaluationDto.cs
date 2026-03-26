using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record EvaluationDto(
    Guid Id,
    Guid UserId,
    IReadOnlyList<Guid> ModelIds,
    string Status,
    EvaluationResultDto? Result,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record EvaluationResultDto(
    Guid WinnerModelId,
    string Reason);

public sealed record LeaderboardEntryDto(
    Guid Id,
    Guid ModelId,
    string ModelName,
    double EloRating,
    int WinCount,
    int LossCount,
    int DrawCount,
    DateTimeOffset LastUpdatedAt);

public sealed record FeedbackDto(
    Guid Id,
    Guid UserId,
    Guid EvaluationId,
    string Rating,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed class EvaluationDtoValidator : AbstractValidator<EvaluationDto>
{
    public EvaluationDtoValidator()
    {
        RuleFor(x => x.ModelIds)
            .NotEmpty().WithMessage("At least one model is required for an evaluation.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Evaluation status is required.");
    }
}

public sealed class EvaluationResultDtoValidator : AbstractValidator<EvaluationResultDto>
{
    public EvaluationResultDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Evaluation result reason is required.");
    }
}

public sealed class FeedbackDtoValidator : AbstractValidator<FeedbackDto>
{
    public FeedbackDtoValidator()
    {
        RuleFor(x => x.Rating)
            .NotEmpty().WithMessage("Feedback rating is required.");
    }
}
