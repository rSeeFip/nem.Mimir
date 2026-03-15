namespace nem.Mimir.Application.McpServers.Dtos;

/// <summary>
/// Data transfer object for an MCP server configuration.
/// </summary>
public sealed record McpServerDto(
    Guid Id,
    string Name,
    string TransportType,
    bool IsEnabled,
    bool IsBundled,
    string? Description,
    string? Command,
    string? Arguments,
    string? Url,
    string? EnvironmentVariablesJson,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<McpToolWhitelistDto> ToolWhitelists);

/// <summary>
/// Data transfer object for a tool whitelist entry.
/// </summary>
public sealed record McpToolWhitelistDto(
    Guid Id,
    string ToolName,
    bool IsEnabled,
    DateTime CreatedAt);

/// <summary>
/// Data transfer object representing a tool exposed by an MCP server.
/// </summary>
public sealed record McpServerToolDto(
    string Name,
    string Description,
    string? ParametersJsonSchema);

/// <summary>
/// Data transfer object for an MCP tool audit log entry.
/// </summary>
public sealed record McpAuditLogDto(
    Guid Id,
    string ToolName,
    string? Input,
    string? Output,
    long LatencyMs,
    bool Success,
    string? ErrorMessage,
    string? UserId,
    Guid? ConversationId,
    DateTime Timestamp);

/// <summary>
/// Data transfer object for MCP server health check result.
/// </summary>
public sealed record McpServerHealthDto(
    Guid ServerId,
    bool IsHealthy,
    string? ErrorMessage);
