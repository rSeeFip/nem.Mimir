using System.Text.Json;

namespace nem.Mimir.Finance.McpTools.Models;

public sealed record FinanceMcpToolDefinition(
    string Name,
    string Description,
    string Action,
    JsonDocument InputSchema,
    JsonDocument OutputSchema);
