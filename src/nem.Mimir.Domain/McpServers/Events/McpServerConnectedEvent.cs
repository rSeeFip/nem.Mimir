namespace nem.Mimir.Domain.McpServers.Events;

/// <summary>
/// Published after an MCP server has been successfully connected.
/// </summary>
public sealed record McpServerConnectedEvent(Guid ServerId, string ServerName);
