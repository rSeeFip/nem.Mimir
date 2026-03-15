using System.Text.Json;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Infrastructure.Tools;

internal sealed class PluginToolProvider : IToolProvider
{
    private readonly IPluginService _pluginService;

    public PluginToolProvider(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public async Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        var plugins = await _pluginService.ListPluginsAsync(cancellationToken);

        return plugins
            .Select(p => new ToolDefinition(p.Id, p.Description))
            .ToList()
            .AsReadOnly();
    }

    public async Task<ToolResult> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var parameters = DeserializeArguments(argumentsJson);
        var context = PluginContext.Create("system", parameters);

        try
        {
            var pluginResult = await _pluginService.ExecutePluginAsync(toolName, context, cancellationToken);

            return pluginResult.IsSuccess
                ? ToolResult.Success(JsonSerializer.Serialize(pluginResult.Data))
                : ToolResult.Failure(pluginResult.ErrorMessage ?? "Plugin execution failed");
        }
        catch (KeyNotFoundException)
        {
            return ToolResult.Failure($"Tool '{toolName}' not found");
        }
    }

    private static IReadOnlyDictionary<string, object> DeserializeArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object>();

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            if (dict is null)
                return new Dictionary<string, object>();

            return dict.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)kvp.Value.ToString()!);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>();
        }
    }
}
