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

    public BuiltInPluginRegistrar(IPluginService pluginService, CodeRunnerPlugin codeRunner)
    {
        _pluginManager = (PluginManager)pluginService;
        _codeRunner = codeRunner;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pluginManager.RegisterPlugin(_codeRunner);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
