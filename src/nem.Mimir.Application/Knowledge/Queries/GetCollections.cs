using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Knowledge.Queries;

public sealed record GetCollectionsQuery() : IQuery<IReadOnlyList<KnowledgeCollectionDto>>;

internal sealed class GetCollectionsQueryHandler
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetCollectionsQueryHandler(
        IKnowledgeCollectionRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<KnowledgeCollectionDto>> Handle(GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var collections = await _repository.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return collections
            .Select(_mapper.MapToKnowledgeCollectionDto)
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
