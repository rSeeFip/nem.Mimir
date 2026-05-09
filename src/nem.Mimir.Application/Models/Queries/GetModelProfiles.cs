using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Models.Queries;

public sealed record GetModelProfilesQuery : IQuery<IReadOnlyList<ModelProfileDto>>;

internal sealed class GetModelProfilesQueryHandler
{
    private readonly IModelProfileRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetModelProfilesQueryHandler(
        IModelProfileRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ModelProfileDto>> Handle(GetModelProfilesQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var profiles = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return profiles
            .Select(_mapper.MapToModelProfileDto)
            .ToList();
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
