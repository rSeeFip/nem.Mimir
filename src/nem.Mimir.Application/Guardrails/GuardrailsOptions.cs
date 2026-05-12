namespace nem.Mimir.Application.Guardrails;

/// <summary>
/// Configuration options for guardrail evaluation.
/// </summary>
public sealed class GuardrailsOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "Guardrails";

    /// <summary>
    /// Enables or disables guardrail evaluation.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Configured output token limit for guardrail-driven output checks.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// Active bundle name resolved from DI when guardrails are enabled.
    /// </summary>
    public string ActiveBundle { get; set; } = "standard";

    /// <summary>
    /// Bundle names that are allowed to be activated from configuration.
    /// </summary>
    public List<string> AvailableBundles { get; set; } = [];
}
