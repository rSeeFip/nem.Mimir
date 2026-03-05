namespace Mimir.Domain.McpServers.Events;

/// <summary>
/// Published after an MCP server has been disconnected.
/// </summary>
public sealed record McpServerDisconnectedEvent(Guid ServerId, string ServerName);
