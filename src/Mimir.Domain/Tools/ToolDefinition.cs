using System.Text.Json;

namespace Mimir.Domain.Tools;

public sealed record ToolDefinition(
    string Name,
    string Description,
    string ServerName,
    JsonDocument? InputSchema);
