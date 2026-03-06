namespace Mimir.Application.Context;

/// <summary>
/// Configuration options for context token optimization behavior.
/// </summary>
public sealed record ContextOptimizerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ContextOptimizer";

    /// <summary>
    /// Desired compression ratio when pruning or distilling content.
    /// Valid range is 0.4 to 0.6.
    /// </summary>
    public double TargetCompressionRatio { get; init; } = 0.5d;

    /// <summary>
    /// Maximum context size in tokens.
    /// </summary>
    public int MaxContextTokens { get; init; } = 8192;

    /// <summary>
    /// Threshold at which optimization auto-triggers as a fraction of max capacity.
    /// Default 0.8 = 80%.
    /// </summary>
    public double AutoTriggerThreshold { get; init; } = 0.8d;

    /// <summary>
    /// Character-per-token heuristic for estimating token count.
    /// </summary>
    public double CharsPerToken { get; init; } = 4.0d;
}
