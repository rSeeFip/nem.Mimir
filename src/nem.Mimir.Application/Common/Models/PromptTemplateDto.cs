using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record PromptTemplateDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Command,
    string Content,
    bool IsShared,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PromptTemplateVersionDto> VersionHistory,
    int UsageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PromptTemplateListDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Command,
    bool IsShared,
    IReadOnlyList<string> Tags,
    int UsageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PromptTemplateVersionDto(
    string Content,
    DateTimeOffset Timestamp);

public sealed class PromptTemplateDtoValidator : AbstractValidator<PromptTemplateDto>
{
    public PromptTemplateDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Prompt template title is required.")
            .MaximumLength(200).WithMessage("Prompt template title must not exceed 200 characters.");

        RuleFor(x => x.Command)
            .NotEmpty().WithMessage("Prompt template command is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Prompt template content is required.");

        RuleFor(x => x.Command)
            .Matches("^/[a-z0-9-]+$").WithMessage("Prompt template command must match '/command-name'.");

        RuleFor(x => x.UsageCount)
            .GreaterThanOrEqualTo(0).WithMessage("Usage count cannot be negative.");
    }
}
