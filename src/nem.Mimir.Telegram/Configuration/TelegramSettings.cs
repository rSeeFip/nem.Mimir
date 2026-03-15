namespace nem.Mimir.Telegram.Configuration;

/// <summary>
/// Configuration settings for the Telegram bot service.
/// </summary>
public sealed class TelegramSettings
{
    public const string SectionName = "Telegram";

    /// <summary>
    /// Gets the Telegram Bot API token.
    /// </summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base URL of the Mimir API.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "http://localhost:5000";

    /// <summary>
    /// Gets the Keycloak token endpoint URL.
    /// </summary>
    public string KeycloakUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Keycloak client ID for obtaining service tokens.
    /// </summary>
    public string KeycloakClientId { get; init; } = "mimir-api";

    /// <summary>
    /// Gets the Keycloak client secret for service-to-service auth.
    /// </summary>
    public string KeycloakClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets the interval in milliseconds between streaming display updates.
    /// </summary>
    public int StreamingUpdateIntervalMs { get; init; } = 500;

    /// <summary>
    /// Gets the auth code expiry duration in minutes.
    /// </summary>
    public int AuthCodeExpiryMinutes { get; init; } = 5;
}
