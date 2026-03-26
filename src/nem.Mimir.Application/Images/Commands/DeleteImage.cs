using FluentValidation;
using ImageGenerationId = nem.Contracts.Identity.ImageGenerationId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Images.Commands;

public sealed record DeleteImageCommand(Guid ImageGenerationId) : ICommand;

public sealed class DeleteImageCommandValidator : AbstractValidator<DeleteImageCommand>
{
    public DeleteImageCommandValidator()
    {
        RuleFor(x => x.ImageGenerationId)
            .NotEmpty().WithMessage("Image generation ID is required.");
    }
}

public sealed class DeleteImageHandler
{
    public async Task Handle(
        DeleteImageCommand command,
        IImageGenerationRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var imageGeneration = await repository.GetByIdAsync(ImageGenerationId.From(command.ImageGenerationId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ImageGeneration), command.ImageGenerationId);

        if (imageGeneration.UserId != userGuid)
            throw new ForbiddenAccessException();

        await repository.DeleteAsync(imageGeneration.Id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
