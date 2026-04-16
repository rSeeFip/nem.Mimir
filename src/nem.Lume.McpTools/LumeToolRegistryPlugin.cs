using System.Text.Json;
using nem.Mimir.Domain.Plugins;

namespace nem.Lume.McpTools;

public sealed class LumeToolRegistryPlugin : IBuiltInPlugin
{
    private static readonly IReadOnlyList<Dictionary<string, object>> ToolPayload = BuildToolPayload();

    public string Id => "mimir.lume.mcp-tools";
    public string Name => "Mimir Lume MCP Tools";
    public string Version => "1.0.0";
    public string Description => "Exposes Lume MCP tool metadata for Mimir tool registry integration.";

    public Task<PluginResult> ExecuteAsync(PluginContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(PluginResult.Success(new Dictionary<string, object>
        {
            ["server"] = "lume",
            ["tools"] = ToolPayload,
        }));
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<Dictionary<string, object>> BuildToolPayload()
    {
        return LumeMcpToolRegistry
            .GetTools()
            .Select(tool => new Dictionary<string, object>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["action"] = tool.Action,
                ["inputSchema"] = Clone(tool.InputSchema),
                ["outputSchema"] = Clone(tool.OutputSchema),
            })
            .ToList()
            .AsReadOnly();
    }

    private static JsonElement Clone(JsonDocument schema)
        => JsonDocument.Parse(schema.RootElement.GetRawText()).RootElement.Clone();
}
