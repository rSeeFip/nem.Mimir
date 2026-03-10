namespace Mimir.Api.Configuration;

/// <summary>
/// Top-level options for the Mimir API.
/// </summary>
public class MimirApiOptions
{
    public const string SectionName = "MimirApiOptions";

    /// <summary>
    /// When true, authentication is handled via a simple standalone handler
    /// (no Keycloak required). Intended for local development only.
    /// </summary>
    public bool StandaloneMode { get; set; }
}
