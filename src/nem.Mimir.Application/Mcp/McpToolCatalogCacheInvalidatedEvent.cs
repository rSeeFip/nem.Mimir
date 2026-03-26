namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Event raised when the MCP tool catalog cache should be invalidated.
/// Can be triggered by MCP server registration/deregistration or
/// published via Wolverine <see cref="Wolverine.IMessageBus"/> for distributed invalidation.
/// </summary>
public sealed record McpToolCatalogCacheInvalidatedEvent(string? Reason = null);
