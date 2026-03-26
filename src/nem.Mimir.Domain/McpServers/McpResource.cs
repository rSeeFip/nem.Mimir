namespace nem.Mimir.Domain.McpServers;

/// <summary>
/// Represents a resource exposed by an MCP server.
/// </summary>
/// <param name="Uri">The URI of the resource.</param>
/// <param name="Name">Human-readable name for the resource.</param>
/// <param name="Description">Optional description of the resource.</param>
/// <param name="MimeType">Optional MIME type of the resource content.</param>
public sealed record McpResourceDefinition(string Uri, string Name, string? Description, string? MimeType);

/// <summary>
/// Represents a resource template exposed by an MCP server.
/// </summary>
/// <param name="UriTemplate">The URI template string.</param>
/// <param name="Name">Human-readable name for the template.</param>
/// <param name="Description">Optional description of the template.</param>
/// <param name="MimeType">Optional MIME type of the resulting resource content.</param>
public sealed record McpResourceTemplateDefinition(string UriTemplate, string Name, string? Description, string? MimeType);

/// <summary>
/// Represents the content of a resource read from an MCP server.
/// </summary>
/// <param name="Uri">The URI of the resource.</param>
/// <param name="MimeType">Optional MIME type of the content.</param>
/// <param name="Text">Text content if the resource is text-based.</param>
/// <param name="Blob">Base64-encoded binary content if the resource is binary.</param>
public sealed record McpResourceContent(string Uri, string? MimeType, string? Text, string? Blob);
