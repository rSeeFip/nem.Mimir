using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record ImageGenerationDto(
    Guid Id,
    Guid UserId,
    string Prompt,
    string? NegativePrompt,
    string Model,
    string Size,
    string Status,
    string? ImageUrl,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ImageGenerationConfigDto(
    string DefaultModel,
    string DefaultSize,
    int DefaultSteps,
    IReadOnlyList<string> AvailableModels);

public sealed class ImageGenerationDtoValidator : AbstractValidator<ImageGenerationDto>
{
    public ImageGenerationDtoValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Image generation prompt is required.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Image generation model is required.");

        RuleFor(x => x.Size)
            .NotEmpty().WithMessage("Image size is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Image generation status is required.");
    }
}

public sealed class ImageGenerationConfigDtoValidator : AbstractValidator<ImageGenerationConfigDto>
{
    public ImageGenerationConfigDtoValidator()
    {
        RuleFor(x => x.DefaultModel)
            .NotEmpty().WithMessage("Default image generation model is required.");

        RuleFor(x => x.DefaultSize)
            .NotEmpty().WithMessage("Default image size is required.");

        RuleFor(x => x.DefaultSteps)
            .GreaterThan(0).WithMessage("Default image generation steps must be greater than 0.");

        RuleFor(x => x.AvailableModels)
            .NotEmpty().WithMessage("At least one available image generation model is required.");
    }
}
