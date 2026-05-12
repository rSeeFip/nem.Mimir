using System.Text.Json;

namespace nem.Mimir.Domain.Tools;

public sealed record ToolDefinition(
    string Name,
    string Description,
    string ServerName,
    JsonDocument? InputSchema);
