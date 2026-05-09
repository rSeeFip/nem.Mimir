namespace nem.Mimir.Api.Configuration;

/// <summary>
/// Configuration settings for the PostgreSQL database connection.
/// </summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>
    /// Gets the database connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
