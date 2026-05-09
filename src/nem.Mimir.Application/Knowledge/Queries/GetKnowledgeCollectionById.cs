using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Knowledge.Queries;

public sealed record GetKnowledgeCollectionByIdQuery(KnowledgeCollectionId Id) : IQuery<KnowledgeCollectionDto>;

internal sealed class GetKnowledgeCollectionByIdQueryHandler
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetKnowledgeCollectionByIdQueryHandler(
        IKnowledgeCollectionRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<KnowledgeCollectionDto> Handle(GetKnowledgeCollectionByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var collection = await _repository.GetByIdForUserAsync(request.Id, userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(KnowledgeCollection), request.Id);

        return _mapper.MapToKnowledgeCollectionDto(collection);
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
