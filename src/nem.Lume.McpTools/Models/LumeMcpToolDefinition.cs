using System.Text.Json;

namespace nem.Lume.McpTools.Models;

public sealed record LumeMcpToolDefinition(
    string Name,
    string Description,
    string Action,
    JsonDocument InputSchema,
    JsonDocument OutputSchema);
