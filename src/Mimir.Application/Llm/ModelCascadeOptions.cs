using nem.Contracts.TokenOptimization;

namespace Mimir.Application.Llm;

/// <summary>
/// Configuration options for the model cascade service.
/// </summary>
public sealed class ModelCascadeOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "ModelCascade";

    /// <summary>
    /// The default model tier to use when no specific tier is determined. Defaults to <see cref="ModelTier.Standard"/>.
    /// </summary>
    public ModelTier DefaultTier { get; set; } = ModelTier.Standard;

    /// <summary>
    /// Whether to enable fallback to higher tiers when the selected tier has no available model. Defaults to <c>true</c>.
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Maximum number of fallback attempts to higher tiers before returning the default model. Defaults to 2.
    /// </summary>
    public int MaxFallbackAttempts { get; set; } = 2;

    /// <summary>
    /// Maps task type strings to their required <see cref="ModelTier"/> values for routing decisions.
    /// </summary>
    public Dictionary<string, ModelTier> TaskTypeComplexityMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
