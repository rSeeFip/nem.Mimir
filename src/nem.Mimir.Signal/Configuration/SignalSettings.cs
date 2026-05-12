namespace nem.Mimir.Signal.Configuration;

/// <summary>
/// Configuration settings for the Signal channel adapter.
/// </summary>
public sealed class SignalSettings
{
    public const string SectionName = "Signal";

    /// <summary>
    /// Gets the base URL of the signal-cli-rest-api instance.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "http://localhost:8080";

    /// <summary>
    /// Gets the registered phone number (E.164 format, e.g. "+1234567890").
    /// </summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the polling interval in milliseconds between receive calls.
    /// </summary>
    public int PollingIntervalMs { get; init; } = 1000;

    /// <summary>
    /// Gets the base URL of the Mimir API for forwarding messages.
    /// </summary>
    public string MimirApiBaseUrl { get; init; } = "http://localhost:5000";
}
