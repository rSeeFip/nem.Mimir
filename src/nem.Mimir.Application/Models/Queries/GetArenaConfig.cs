using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Models.Queries;

public sealed record GetArenaConfigQuery : IQuery<ArenaConfigDto>;

internal sealed class GetArenaConfigQueryHandler
{
    private readonly IArenaConfigRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetArenaConfigQueryHandler(
        IArenaConfigRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<ArenaConfigDto> Handle(GetArenaConfigQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var config = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            return new ArenaConfigDto(
                Guid.Empty,
                userId,
                Array.Empty<string>(),
                IsBlindComparisonEnabled: true,
                ShowModelNamesAfterVote: true,
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: null);
        }

        return _mapper.MapToArenaConfigDto(config);
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
