using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Knowledge.Queries;

public sealed record SearchCollectionQuery(
    KnowledgeCollectionId CollectionId,
    string Query,
    int MaxResults = 10) : IQuery<IReadOnlyList<KnowledgeSearchResultDto>>;

public sealed class SearchCollectionQueryValidator : AbstractValidator<SearchCollectionQuery>
{
    public SearchCollectionQueryValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection ID is required.");

        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query is required.")
            .MaximumLength(1000).WithMessage("Search query must not exceed 1000 characters.");

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 100).WithMessage("Max results must be between 1 and 100.");
    }
}

internal sealed class SearchCollectionQueryHandler
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IKnowHubBridge _knowHubBridge;

    public SearchCollectionQueryHandler(
        IKnowledgeCollectionRepository repository,
        ICurrentUserService currentUserService,
        IKnowHubBridge knowHubBridge)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _knowHubBridge = knowHubBridge;
    }

    public async Task<IReadOnlyList<KnowledgeSearchResultDto>> Handle(SearchCollectionQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var collection = await _repository.GetByIdForUserAsync(request.CollectionId, userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(KnowledgeCollection), request.CollectionId);

        var allowedDocumentIds = collection.Documents.Select(d => d.DocumentId).ToHashSet();
        var results = await _knowHubBridge
            .SearchKnowledgeAsync(request.Query, request.MaxResults, cancellationToken)
            .ConfigureAwait(false);

        return results
            .Where(x => allowedDocumentIds.Contains(x.ContentId))
            .Select(x => new KnowledgeSearchResultDto(
                x.ContentId,
                x.ChunkText,
                x.Similarity,
                x.EntityType,
                x.EntityId))
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
