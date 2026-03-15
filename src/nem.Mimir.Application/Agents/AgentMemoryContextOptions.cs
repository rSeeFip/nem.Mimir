namespace nem.Mimir.Application.Agents;

public sealed class AgentMemoryContextOptions
{
    public const string SectionName = "AgentMemoryContext";

    public int MemoryRetrievalTimeoutMs { get; set; } = 200;

    public int SemanticBudgetPercent { get; set; } = 20;

    public int EpisodicBudgetPercent { get; set; } = 30;

    public int WorkingBudgetPercent { get; set; } = 50;

    public float EstimatedTokensPerChar { get; set; } = 0.25f;
}
