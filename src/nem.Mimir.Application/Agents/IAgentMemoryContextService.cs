namespace nem.Mimir.Application.Agents;

/// <summary>
/// Assembles a composite memory context for an agent from semantic, episodic, and working memory stores.
/// </summary>
public interface IAgentMemoryContextService
{
    Task<MemoryContext> AssembleContextAsync(
        string conversationId,
        string userId,
        string agentType,
        int maxTokenBudget,
        CancellationToken ct = default);
}

/// <summary>
/// Assembled memory context containing facts, episode summaries, and working messages within a token budget.
/// </summary>
public sealed record MemoryContext(
    IReadOnlyList<string> SemanticFacts,
    IReadOnlyList<string> EpisodicSummaries,
    IReadOnlyList<string> WorkingMessages,
    int TotalTokenEstimate);
