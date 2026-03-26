using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record ModelProfileDto(
    Guid Id,
    Guid UserId,
    string Name,
    string ModelId,
    decimal? Temperature,
    decimal? TopP,
    int? MaxTokens,
    decimal? FrequencyPenalty,
    decimal? PresencePenalty,
    IReadOnlyList<string> StopSequences,
    string? SystemPromptOverride,
    string? ResponseFormat,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ModelCapabilityDto(
    string ModelId,
    bool SupportsVision,
    bool SupportsFunctionCalling,
    bool SupportsStreaming,
    bool SupportsJsonMode);

public sealed record ArenaConfigDto(
    Guid Id,
    Guid UserId,
    IReadOnlyList<string> ModelIds,
    bool IsBlindComparisonEnabled,
    bool ShowModelNamesAfterVote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class ModelProfileDtoValidator : AbstractValidator<ModelProfileDto>
{
    public ModelProfileDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Profile name is required.")
            .MaximumLength(200).WithMessage("Profile name must not exceed 200 characters.");

        RuleFor(x => x.ModelId)
            .NotEmpty().WithMessage("Model ID is required.")
            .MaximumLength(200).WithMessage("Model ID must not exceed 200 characters.");
    }
}
