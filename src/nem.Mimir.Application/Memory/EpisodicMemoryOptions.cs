namespace nem.Mimir.Application.Services.Memory;

public sealed record EpisodicMemoryOptions
{
    public const string SectionName = "EpisodicMemory";

    public int MaxEpisodesPerUser { get; init; } = 1000;

    public int EmbeddingDimension { get; init; } = 1024;

    public float MinQualityScore { get; init; } = 0.4f;
}
