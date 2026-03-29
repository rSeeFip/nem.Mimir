using Microsoft.Extensions.Hosting;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Finance.McpTools;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Hosted service that registers built-in plugins with the <see cref="PluginManager"/> at startup.
/// </summary>
internal sealed class BuiltInPluginRegistrar : IHostedService
{
    private readonly PluginManager _pluginManager;
    private readonly CodeRunnerPlugin _codeRunner;
    private readonly WebSearchPlugin _webSearch;
    private readonly FinanceToolRegistryPlugin _financeTools;

    public BuiltInPluginRegistrar(
        IPluginService pluginService,
        CodeRunnerPlugin codeRunner,
        WebSearchPlugin webSearch,
        FinanceToolRegistryPlugin financeTools)
    {
        _pluginManager = (PluginManager)pluginService;
        _codeRunner = codeRunner;
        _webSearch = webSearch;
        _financeTools = financeTools;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pluginManager.RegisterPlugin(_codeRunner);
        _pluginManager.RegisterPlugin(_webSearch);
        _pluginManager.RegisterPlugin(_financeTools);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
