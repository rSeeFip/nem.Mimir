namespace nem.Mimir.Application.Tokens;

/// <summary>
/// Configuration options for the token tracking service.
/// </summary>
public sealed class TokenTrackerOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "TokenTracker";

    /// <summary>
    /// Whether token tracking is enabled. When disabled, recording and queries return early.
    /// </summary>
    public bool EnableTracking { get; set; } = true;

    /// <summary>
    /// Default budget per service in currency units. Null means unlimited.
    /// </summary>
    public decimal? DefaultBudgetPerService { get; set; }

    /// <summary>
    /// Threshold (0.0–1.0) at which a budget warning is logged. Defaults to 0.8 (80%).
    /// </summary>
    public double BudgetWarningThreshold { get; set; } = 0.8;
}
