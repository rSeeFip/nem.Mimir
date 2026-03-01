using Microsoft.Extensions.Hosting;
using Mimir.Application.Common.Interfaces;

namespace Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Hosted service that registers built-in plugins with the <see cref="PluginManager"/> at startup.
/// </summary>
internal sealed class BuiltInPluginRegistrar : IHostedService
{
    private readonly PluginManager _pluginManager;
    private readonly CodeRunnerPlugin _codeRunner;
    private readonly WebSearchPlugin _webSearch;

    public BuiltInPluginRegistrar(IPluginService pluginService, CodeRunnerPlugin codeRunner, WebSearchPlugin webSearch)
    {
        _pluginManager = (PluginManager)pluginService;
        _codeRunner = codeRunner;
        _webSearch = webSearch;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pluginManager.RegisterPlugin(_codeRunner);
        _pluginManager.RegisterPlugin(_webSearch);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
