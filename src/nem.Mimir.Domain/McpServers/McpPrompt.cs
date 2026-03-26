namespace nem.Mimir.Domain.McpServers;

/// <summary>
/// Represents a prompt exposed by an MCP server.
/// </summary>
/// <param name="Name">Unique name of the prompt.</param>
/// <param name="Description">Optional description of what the prompt does.</param>
/// <param name="Arguments">Optional list of arguments the prompt accepts.</param>
public sealed record McpPromptDefinition(string Name, string? Description, IReadOnlyList<McpPromptArgument>? Arguments);

/// <summary>
/// Represents an argument that a prompt accepts.
/// </summary>
/// <param name="Name">Name of the argument.</param>
/// <param name="Description">Optional description of the argument.</param>
/// <param name="Required">Whether this argument is required.</param>
public sealed record McpPromptArgument(string Name, string? Description, bool Required);

/// <summary>
/// Represents the result of getting a prompt from an MCP server.
/// </summary>
/// <param name="Description">Optional description of the prompt result.</param>
/// <param name="Messages">The prompt messages returned by the server.</param>
public sealed record McpPromptResult(string? Description, IReadOnlyList<McpPromptMessage> Messages);

/// <summary>
/// Represents a single message in a prompt result.
/// </summary>
/// <param name="Role">The role of the message (e.g., "user", "assistant").</param>
/// <param name="Content">The text content of the message.</param>
public sealed record McpPromptMessage(string Role, string Content);
