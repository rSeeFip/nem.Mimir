using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Conversations.Services;

public interface IConversationKnowledgeProvider
{
    Task<IReadOnlyList<KnowledgeSearchResultDto>> SearchConversationKnowledgeAsync(Guid conversationId, string query, CancellationToken cancellationToken = default);
}

public interface IConversationContextService
{
    Task<IReadOnlyList<KnowledgeSearchResultDto>> GetRagContextAsync(Guid conversationId, string query, CancellationToken cancellationToken = default);
}

public sealed class ConversationRagService : IConversationContextService
{
    private readonly IConversationKnowledgeProvider _knowledgeProvider;

    public ConversationRagService(IConversationKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public Task<IReadOnlyList<KnowledgeSearchResultDto>> GetRagContextAsync(Guid conversationId, string query, CancellationToken cancellationToken = default)
        => _knowledgeProvider.SearchConversationKnowledgeAsync(conversationId, query, cancellationToken);
}
