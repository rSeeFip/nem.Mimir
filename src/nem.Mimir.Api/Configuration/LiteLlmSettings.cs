namespace nem.Mimir.Api.Configuration;

/// <summary>
/// Configuration settings for the LiteLLM proxy service.
/// </summary>
public sealed class LiteLlmSettings
{
    public const string SectionName = "LiteLlm";

    /// <summary>
    /// Gets the base URL of the LiteLLM proxy.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the API key for authenticating with LiteLLM.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timeout in seconds for LiteLLM requests.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;
}
