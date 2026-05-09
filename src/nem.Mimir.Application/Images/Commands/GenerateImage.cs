using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Images.Commands;

public sealed record GenerateImageCommand(
    string Prompt,
    string? NegativePrompt,
    string Model,
    string Size,
    string? Quality,
    int NumberOfImages = 1) : ICommand<ImageGenerationDto>;

public sealed class GenerateImageCommandValidator : AbstractValidator<GenerateImageCommand>
{
    public GenerateImageCommandValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required.")
            .MaximumLength(4000).WithMessage("Prompt must not exceed 4000 characters.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required.")
            .MaximumLength(100).WithMessage("Model must not exceed 100 characters.");

        RuleFor(x => x.Size)
            .NotEmpty().WithMessage("Size is required.")
            .MaximumLength(32).WithMessage("Size must not exceed 32 characters.");

        RuleFor(x => x.NumberOfImages)
            .InclusiveBetween(1, 10).WithMessage("Number of images must be between 1 and 10.");
    }
}

public sealed class GenerateImageHandler
{
    public async Task<ImageGenerationDto> Handle(
        GenerateImageCommand command,
        IImageGenerationRepository repository,
        IImageGenerationService imageGenerationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var imageGeneration = ImageGeneration.Create(
            userGuid,
            command.Prompt,
            command.NegativePrompt,
            command.Model,
            command.Size,
            command.Quality,
            command.NumberOfImages);

        imageGeneration.MarkProcessing();

        var imageUrl = await imageGenerationService
            .GenerateAsync(command.Prompt, command.NegativePrompt, command.Model, command.Size, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            imageGeneration.MarkCompleted(imageUrl);
        }
        else
        {
            imageGeneration.MarkFailed("Image generation service did not return an image URL.");
        }

        await repository.CreateAsync(imageGeneration, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToImageGenerationDto(imageGeneration);
    }
}
