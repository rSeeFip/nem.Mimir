namespace nem.Mimir.WhatsApp.Configuration;

/// <summary>
/// Configuration settings for the WhatsApp Cloud API channel adapter.
/// </summary>
public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    /// <summary>
    /// Gets the WhatsApp Cloud API access token.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the WhatsApp Business Account phone number ID.
    /// </summary>
    public string PhoneNumberId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the webhook verification token for challenge/response.
    /// </summary>
    public string VerifyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the app secret used for HMAC-SHA256 webhook signature validation.
    /// </summary>
    public string AppSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets the WhatsApp Cloud API base URL.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "https://graph.facebook.com/v21.0";

    /// <summary>
    /// Gets the base URL of the Mimir API.
    /// </summary>
    public string MimirApiBaseUrl { get; init; } = "http://localhost:5000";
}
