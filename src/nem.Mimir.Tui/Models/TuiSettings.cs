namespace nem.Mimir.Tui.Models;

/// <summary>
/// Configuration settings for the TUI application.
/// </summary>
internal sealed class TuiSettings
{
    public const string SectionName = "MimirTui";

    /// <summary>
    /// Gets the base URL for the Mimir REST API.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "http://localhost:5000";

    /// <summary>
    /// Gets the URL for the SignalR ChatHub.
    /// </summary>
    public string HubUrl { get; init; } = "http://localhost:5000/hubs/chat";

    /// <summary>
    /// Gets the Keycloak token endpoint URL.
    /// </summary>
    public string KeycloakUrl { get; init; } = "http://localhost:8080/realms/mimir/protocol/openid-connect/token";

    /// <summary>
    /// Gets the Keycloak client identifier.
    /// </summary>
    public string KeycloakClientId { get; init; } = "mimir-api";

    /// <summary>
    /// Gets the default LLM model to use.
    /// </summary>
    public string DefaultModel { get; init; } = "phi-4-mini";
}
