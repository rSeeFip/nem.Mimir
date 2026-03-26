using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Services;
using nem.Mimir.Application.Knowledge;

namespace nem.Mimir.Infrastructure.Knowledge;

internal sealed class ConversationKnowledgeProvider : IConversationKnowledgeProvider
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IKnowledgeCollectionRepository _knowledgeCollectionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IKnowHubBridge _knowHubBridge;

    public ConversationKnowledgeProvider(
        IConversationRepository conversationRepository,
        IKnowledgeCollectionRepository knowledgeCollectionRepository,
        ICurrentUserService currentUserService,
        IKnowHubBridge knowHubBridge)
    {
        _conversationRepository = conversationRepository;
        _knowledgeCollectionRepository = knowledgeCollectionRepository;
        _currentUserService = currentUserService;
        _knowHubBridge = knowHubBridge;
    }

    public async Task<IReadOnlyList<KnowledgeSearchResultDto>> SearchConversationKnowledgeAsync(Guid conversationId, string query, CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Conversation), conversationId);

        if (conversation.UserId != userId)
            throw new ForbiddenAccessException();

        var collections = await _knowledgeCollectionRepository.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var collection = collections.FirstOrDefault(item =>
            string.Equals(item.Name, ConversationKnowledgeCollectionNaming.BuildName(conversationId), StringComparison.OrdinalIgnoreCase));

        if (collection is null || collection.Documents.Count == 0)
            return [];

        var allowedDocumentIds = collection.Documents.Select(document => document.DocumentId).ToHashSet();
        var results = await _knowHubBridge.SearchKnowledgeAsync(query, 10, cancellationToken).ConfigureAwait(false);

        return results
            .Where(result => allowedDocumentIds.Contains(result.ContentId))
            .Select(result => new KnowledgeSearchResultDto(
                result.ContentId,
                result.ChunkText,
                result.Similarity,
                result.EntityType,
                result.EntityId))
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
