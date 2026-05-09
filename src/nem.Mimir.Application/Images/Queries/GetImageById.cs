using FluentValidation;
using ImageGenerationId = nem.Contracts.Identity.ImageGenerationId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Images.Queries;

public sealed record GetImageByIdQuery(Guid ImageGenerationId) : IQuery<ImageGenerationDto>;

public sealed class GetImageByIdQueryValidator : AbstractValidator<GetImageByIdQuery>
{
    public GetImageByIdQueryValidator()
    {
        RuleFor(x => x.ImageGenerationId)
            .NotEmpty().WithMessage("Image generation ID is required.");
    }
}

public sealed class GetImageByIdHandler
{
    public async Task<ImageGenerationDto> Handle(
        GetImageByIdQuery query,
        IImageGenerationRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var imageGeneration = await repository.GetByIdAsync(ImageGenerationId.From(query.ImageGenerationId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ImageGeneration), query.ImageGenerationId);

        if (imageGeneration.UserId != userGuid)
            throw new ForbiddenAccessException();

        return mapper.MapToImageGenerationDto(imageGeneration);
    }
}
