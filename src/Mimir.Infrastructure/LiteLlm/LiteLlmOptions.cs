namespace Mimir.Infrastructure.LiteLlm;

/// <summary>
/// Configuration options for the LiteLLM HTTP client.
/// Bound from the "LiteLlm" configuration section.
/// </summary>
public sealed class LiteLlmOptions
{
    public const string SectionName = "LiteLlm";

    /// <summary>
    /// Gets the base URL of the LiteLLM proxy (e.g., "http://localhost:4000").
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:4000";

    /// <summary>
    /// Gets the API key for authenticating with LiteLLM.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timeout in seconds for LiteLLM requests.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Gets the default model to use when none is specified.
    /// </summary>
    public string DefaultModel { get; init; } = LlmModels.Default;

    /// <summary>
    /// Gets the maximum number of requests that can be queued.
    /// </summary>
    public int MaxQueueSize { get; init; } = 100;
}
