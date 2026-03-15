namespace nem.Mimir.Application.Services.Memory;

public sealed record MemoryConsolidationOptions
{
    public const string SectionName = "MemoryConsolidation";

    public float EpisodicToSemanticThreshold { get; init; } = 0.6f;

    public int MaxEpisodesPerConsolidation { get; init; } = 50;

    public bool EnableCrossUserInsights { get; init; } = false;
}
