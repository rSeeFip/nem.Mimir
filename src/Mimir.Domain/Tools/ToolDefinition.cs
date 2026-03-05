namespace Mimir.Domain.Tools;

/// <summary>
/// Describes a single tool that can be offered to an LLM for function calling.
/// </summary>
/// <param name="Name">Unique tool identifier (e.g. plugin ID or MCP tool name).</param>
/// <param name="Description">Human-readable description for the LLM.</param>
/// <param name="ParametersJsonSchema">Optional JSON Schema describing accepted parameters.</param>
public sealed record ToolDefinition(string Name, string Description, string? ParametersJsonSchema = null);
