using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Images.Queries;

public sealed record GetImageGenerationsQuery(int PageNumber, int PageSize) : IQuery<PaginatedList<ImageGenerationDto>>;

public sealed class GetImageGenerationsQueryValidator : AbstractValidator<GetImageGenerationsQuery>
{
    public GetImageGenerationsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetImageGenerationsHandler
{
    public async Task<PaginatedList<ImageGenerationDto>> Handle(
        GetImageGenerationsQuery query,
        IImageGenerationRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var result = await repository.GetByUserIdAsync(userGuid, query.PageNumber, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToImageGenerationDto).ToList();
        return new PaginatedList<ImageGenerationDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
