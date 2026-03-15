namespace nem.Mimir.Infrastructure.McpServers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.McpServers;

internal sealed class McpClientStartupService : IHostedService
{
    private readonly IMcpClientManager _clientManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<McpClientStartupService> _logger;

    public McpClientStartupService(
        IMcpClientManager clientManager,
        IServiceScopeFactory scopeFactory,
        ILogger<McpClientStartupService> logger)
    {
        _clientManager = clientManager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auto-connecting enabled MCP servers...");

        IReadOnlyList<McpServerConfig> enabledConfigs;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IMcpServerConfigRepository>();
            enabledConfigs = await repo.GetEnabledAsync(cancellationToken);
        }

        var connected = 0;
        foreach (var config in enabledConfigs)
        {
            try
            {
                await _clientManager.ConnectAsync(config, cancellationToken);
                connected++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-connect MCP server {Name} ({ServerId})", config.Name, config.Id);
            }
        }

        _logger.LogInformation("Auto-connected {Connected}/{Total} enabled MCP servers", connected, enabledConfigs.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disconnecting all MCP servers...");

        var servers = await _clientManager.GetConnectedServersAsync(cancellationToken);
        foreach (var server in servers)
        {
            try
            {
                await _clientManager.DisconnectAsync(server.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting MCP server {Name} ({ServerId})", server.Name, server.Id);
            }
        }
    }
}
