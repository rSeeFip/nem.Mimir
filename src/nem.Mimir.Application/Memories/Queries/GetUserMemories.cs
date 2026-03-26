using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Memories.Queries;

public sealed record GetUserMemoriesQuery : IQuery<IReadOnlyList<UserMemoryDto>>;

internal sealed class GetUserMemoriesQueryHandler
{
    private readonly IUserMemoryRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetUserMemoriesQueryHandler(
        IUserMemoryRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<UserMemoryDto>> Handle(GetUserMemoriesQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var memories = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return memories
            .Select(_mapper.MapToUserMemoryDto)
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
