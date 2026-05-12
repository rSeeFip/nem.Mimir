using System.Text.Json;

namespace nem.Mimir.McpServer;

/// <summary>
/// Shared JSON serialization options for MCP tool responses.
/// </summary>
internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
