namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Configuration options for the MCP tool catalog service.
/// </summary>
public sealed class McpToolCatalogOptions
{
    /// <summary>
    /// Cache time-to-live for the aggregated tool catalog.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The cache key used for storing the aggregated tool list.
    /// </summary>
    public string CacheKey { get; set; } = "mcp:tool-catalog:all";
}
