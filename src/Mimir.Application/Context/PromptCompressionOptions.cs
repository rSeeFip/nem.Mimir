namespace Mimir.Application.Context;

public sealed record PromptCompressionOptions
{
    public const string SectionName = "PromptCompression";

    public int MaxFewShotExamples { get; init; } = 3;

    public float ToolDescriptionRelevanceThreshold { get; init; } = 0.3f;

    public bool CompressSystemPrompts { get; init; } = true;

    public bool PruneFewShotExamples { get; init; } = true;

    public int MaxToolDescriptionTokens { get; init; } = 500;
}
