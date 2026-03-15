namespace nem.Mimir.Application.Services.Memory;

public sealed record SemanticMemoryOptions
{
    public const string SectionName = "SemanticMemory";

    public int MaxFactsPerUser { get; init; } = 10000;

    public float MinConfidence { get; init; } = 0.3f;

    public int GraphTraversalMaxDepth { get; init; } = 3;

    public int EmbeddingDimension { get; init; } = 1024;
}
