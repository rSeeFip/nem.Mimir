namespace Mimir.Application.Agents;

public interface IAgentMemoryContextService
{
    Task<MemoryContext> AssembleContextAsync(
        string conversationId,
        string userId,
        string agentType,
        int maxTokenBudget,
        CancellationToken ct = default);
}

public sealed record MemoryContext(
    IReadOnlyList<string> SemanticFacts,
    IReadOnlyList<string> EpisodicSummaries,
    IReadOnlyList<string> WorkingMessages,
    int TotalTokenEstimate);
