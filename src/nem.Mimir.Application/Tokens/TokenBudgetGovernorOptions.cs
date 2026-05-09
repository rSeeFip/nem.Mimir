namespace nem.Mimir.Application.Tokens;

/// <summary>
/// Configuration options for token budget governance.
/// </summary>
public sealed class TokenBudgetGovernorOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "TokenBudgetGovernor";

    /// <summary>
    /// Default budget in tokens applied when no tenant override exists.
    /// </summary>
    public long DefaultBudget { get; set; }

    /// <summary>
    /// Percent of budget consumption that should produce a warning.
    /// </summary>
    public double WarnThresholdPercent { get; set; } = 80.0;

    /// <summary>
    /// Per-tenant budget overrides keyed by tenant identifier.
    /// </summary>
    public Dictionary<string, long> TenantOverrides { get; set; } = new(StringComparer.Ordinal);
}
