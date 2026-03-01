using Mimir.Domain.Plugins;

namespace Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Built-in web search plugin stub. Always returns a "not configured" result
/// until a search provider is configured.
/// </summary>
internal sealed class WebSearchPlugin : IPlugin
{
    public string Id => "mimir.builtin.web-search";
    public string Name => "Web Search";
    public string Version => "1.0.0";
    public string Description => "Searches the web for information. Requires a search provider to be configured.";

    public Task<PluginResult> ExecuteAsync(PluginContext context, CancellationToken ct = default)
    {
        if (!context.Parameters.TryGetValue("query", out var queryObj) || queryObj is not string query || string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(PluginResult.Failure("Required parameter 'query' is missing or empty."));
        }

        return Task.FromResult(
            PluginResult.Failure("Web search plugin is not yet configured. Please configure a search provider in the application settings."));
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
