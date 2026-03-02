namespace Mimir.Api.Configuration;

/// <summary>
/// Configuration settings for JWT authentication via Keycloak.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets the Keycloak realm URL used as the token authority.
    /// </summary>
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected audience for JWT tokens.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether HTTPS is required for metadata retrieval.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; }

    /// <summary>
    /// Validates JWT settings at startup to prevent silent misconfiguration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Authority))
            throw new InvalidOperationException("JwtSettings:Authority is required. Configure 'Jwt:Authority' in appsettings or environment variables.");

        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("JwtSettings:Audience is required. Configure 'Jwt:Audience' in appsettings or environment variables.");
    }
}
