namespace Mimir.Infrastructure.McpServers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mimir.Domain.McpServers;

/// <summary>
/// Background service that polls the database for MCP server configuration changes
/// and diffs the state to connect added, disconnect removed, and reconnect modified servers.
/// </summary>
internal sealed class McpConfigChangeListener : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpClientManager _clientManager;
    private readonly ILogger<McpConfigChangeListener> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(5);

    /// <summary>Tracks the last known config state for diffing.</summary>
    private Dictionary<Guid, McpServerConfig> _lastKnownState = new();

    public McpConfigChangeListener(
        IServiceScopeFactory scopeFactory,
        IMcpClientManager clientManager,
        ILogger<McpConfigChangeListener> logger)
    {
        _scopeFactory = scopeFactory;
        _clientManager = clientManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait to let startup service finish initial connections
        await Task.Delay(_startupDelay, stoppingToken);

        await InitializeStateAsync(stoppingToken);

        _logger.LogInformation("MCP config change listener started (polling every {Interval}s)", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
                await CheckForChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MCP config change polling");
            }
        }
    }

    /// <summary>
    /// Public entry point for event-driven refresh (called by Wolverine handler).
    /// </summary>
    public Task TriggerRefreshAsync(CancellationToken ct = default)
        => CheckForChangesAsync(ct);

    internal async Task CheckForChangesAsync(CancellationToken ct)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMcpServerConfigRepository>();
            var currentConfigs = await repo.GetAllAsync(ct);
            var currentState = currentConfigs.ToDictionary(c => c.Id);

            var added = currentState.Keys.Except(_lastKnownState.Keys).ToList();
            var removed = _lastKnownState.Keys.Except(currentState.Keys).ToList();
            var modified = currentState.Keys.Intersect(_lastKnownState.Keys)
                .Where(id => HasConfigChanged(currentState[id], _lastKnownState[id]))
                .ToList();

            // Connect newly added (if enabled)
            foreach (var id in added)
            {
                var config = currentState[id];
                if (config.IsEnabled)
                {
                    try
                    {
                        await _clientManager.ConnectAsync(config, ct);
                        _logger.LogInformation("Connected new MCP server: {Name} ({ServerId})", config.Name, id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to connect new MCP server: {Name} ({ServerId})", config.Name, id);
                    }
                }
            }

            // Disconnect removed
            foreach (var id in removed)
            {
                try
                {
                    await _clientManager.DisconnectAsync(id, ct);
                    _logger.LogInformation("Disconnected removed MCP server: {Name} ({ServerId})", _lastKnownState[id].Name, id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to disconnect removed MCP server: {Name} ({ServerId})", _lastKnownState[id].Name, id);
                }
            }

            // Reconnect modified (disconnect + reconnect if enabled)
            foreach (var id in modified)
            {
                var config = currentState[id];
                try
                {
                    await _clientManager.DisconnectAsync(id, ct);

                    if (config.IsEnabled)
                    {
                        await _clientManager.ConnectAsync(config, ct);
                        _logger.LogInformation("Reconnected modified MCP server: {Name} ({ServerId})", config.Name, id);
                    }
                    else
                    {
                        _logger.LogInformation("Disconnected disabled MCP server: {Name} ({ServerId})", config.Name, id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect modified MCP server: {Name} ({ServerId})", config.Name, id);
                }
            }

            _lastKnownState = currentState;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task InitializeStateAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMcpServerConfigRepository>();
            var configs = await repo.GetAllAsync(ct);
            _lastKnownState = configs.ToDictionary(c => c.Id);
            _logger.LogInformation("Initialized MCP config state with {Count} servers", _lastKnownState.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MCP config state");
        }
    }

    internal static bool HasConfigChanged(McpServerConfig current, McpServerConfig previous)
    {
        return current.IsEnabled != previous.IsEnabled
            || current.TransportType != previous.TransportType
            || current.Command != previous.Command
            || current.Arguments != previous.Arguments
            || current.Url != previous.Url
            || current.EnvironmentVariablesJson != previous.EnvironmentVariablesJson
            || current.UpdatedAt != previous.UpdatedAt;
    }
}
