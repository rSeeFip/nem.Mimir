namespace Mimir.Domain.McpServers.Events;

/// <summary>
/// Wolverine message — published when API creates/updates/deletes MCP server config.
/// </summary>
public sealed record McpConfigUpdatedEvent(Guid? ServerId, string ChangeType);
