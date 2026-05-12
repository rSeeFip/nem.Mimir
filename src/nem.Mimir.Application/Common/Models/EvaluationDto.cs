using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record EvaluationDto(
    Guid Id,
    Guid ModelAId,
    Guid ModelBId,
    string Prompt,
    string ResponseA,
    string ResponseB,
    string Winner,
    string Status,
    Guid UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record LeaderboardEntryDto(
    Guid Id,
    Guid ModelId,
    decimal EloScore,
    int Wins,
    int Losses,
    int Draws,
    int TotalEvaluations,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EvaluationFeedbackDto(
    Guid Id,
    Guid EvaluationId,
    Guid UserId,
    int Quality,
    int Relevance,
    int Accuracy,
    decimal Score,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class EvaluationDtoValidator : AbstractValidator<EvaluationDto>
{
    public EvaluationDtoValidator()
    {
        RuleFor(x => x.ModelAId)
            .NotEmpty().WithMessage("Model A is required.");

        RuleFor(x => x.ModelBId)
            .NotEmpty().WithMessage("Model B is required.");

        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Evaluation status is required.");
    }
}

public sealed class LeaderboardEntryDtoValidator : AbstractValidator<LeaderboardEntryDto>
{
    public LeaderboardEntryDtoValidator()
    {
        RuleFor(x => x.ModelId)
            .NotEmpty().WithMessage("Model ID is required.");

        RuleFor(x => x.EloScore)
            .GreaterThan(0m).WithMessage("Elo score must be positive.");
    }
}

public sealed class EvaluationFeedbackDtoValidator : AbstractValidator<EvaluationFeedbackDto>
{
    public EvaluationFeedbackDtoValidator()
    {
        RuleFor(x => x.EvaluationId)
            .NotEmpty().WithMessage("Evaluation ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Quality)
            .InclusiveBetween(1, 5).WithMessage("Quality rating must be between 1 and 5.");

        RuleFor(x => x.Relevance)
            .InclusiveBetween(1, 5).WithMessage("Relevance rating must be between 1 and 5.");

        RuleFor(x => x.Accuracy)
            .InclusiveBetween(1, 5).WithMessage("Accuracy rating must be between 1 and 5.");
    }
}
