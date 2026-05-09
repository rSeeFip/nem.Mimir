using nem.Contracts.Inference;

namespace nem.Mimir.Infrastructure.Inference;

/// <summary>
/// Configuration options for the inference policy.
/// </summary>
public sealed class InferencePolicyOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "InferencePolicy";

    /// <summary>
    /// Enables or disables policy evaluation.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Configured default policy name.
    /// </summary>
    public string DefaultPolicy { get; set; } = "allow-all";

    /// <summary>
    /// The policy action returned by the default inference policy.
    /// </summary>
    public PolicyAction Action { get; set; } = PolicyAction.Allow;

    /// <summary>
    /// Optional redirect alias when the configured action is redirect.
    /// </summary>
    public string? RedirectAlias { get; set; }

    /// <summary>
    /// Optional denial reason returned when the configured action is deny.
    /// </summary>
    public string? DenialReason { get; set; }
}
