namespace Mimir.Infrastructure.McpServers;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Mimir.Domain.McpServers;
using Mimir.Domain.Tools;

internal sealed class McpClientEntry
{
    public required McpClient Client { get; init; }
    public required McpServerConfig Config { get; init; }
}

internal sealed class McpClientManager : IMcpClientManager, IAsyncDisposable
{
    private static readonly TimeSpan DefaultToolTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);
    private const int MaxRetries = 3;

    private readonly ConcurrentDictionary<Guid, McpClientEntry> _clients = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpClientManager> _logger;

    public McpClientManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpClientManager>();
    }

    public async Task ConnectAsync(McpServerConfig config, CancellationToken ct = default)
    {
        if (_clients.ContainsKey(config.Id))
        {
            _logger.LogWarning("Server {ServerId} ({Name}) is already connected, disconnecting first", config.Id, config.Name);
            await DisconnectAsync(config.Id, ct);
        }

        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Connecting to MCP server {Name} (attempt {Attempt}/{Max})", config.Name, attempt, MaxRetries);

                var transport = CreateTransport(config);
                var options = new McpClientOptions
                {
                    ClientInfo = new Implementation { Name = "Mimir", Version = "1.0.0" },
                };

                var client = await McpClient.CreateAsync(transport, options, _loggerFactory, ct);

                var entry = new McpClientEntry { Client = client, Config = config };
                _clients[config.Id] = entry;

                _logger.LogInformation("Connected to MCP server {Name} ({ServerId})", config.Name, config.Id);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Failed to connect to MCP server {Name} (attempt {Attempt}/{Max}), retrying in {Delay}s",
                    config.Name, attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }

        // Final attempt — let exceptions propagate
        {
            _logger.LogInformation("Connecting to MCP server {Name} (attempt {Attempt}/{Max})", config.Name, MaxRetries, MaxRetries);

            var transport = CreateTransport(config);
            var options = new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "Mimir", Version = "1.0.0" },
            };

            var client = await McpClient.CreateAsync(transport, options, _loggerFactory, ct);

            var entry = new McpClientEntry { Client = client, Config = config };
            _clients[config.Id] = entry;

            _logger.LogInformation("Connected to MCP server {Name} ({ServerId})", config.Name, config.Id);
        }
    }

    public async Task DisconnectAsync(Guid serverId, CancellationToken ct = default)
    {
        if (!_clients.TryRemove(serverId, out var entry))
        {
            _logger.LogWarning("Server {ServerId} is not connected", serverId);
            return;
        }

        try
        {
            await entry.Client.DisposeAsync();
            _logger.LogInformation("Disconnected from MCP server {Name} ({ServerId})", entry.Config.Name, serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MCP client for server {Name} ({ServerId})", entry.Config.Name, serverId);
        }
    }

    public Task<IReadOnlyList<McpServerConfig>> GetConnectedServersAsync(CancellationToken ct = default)
    {
        var configs = _clients.Values.Select(e => e.Config).ToList();
        return Task.FromResult<IReadOnlyList<McpServerConfig>>(configs);
    }

    public async Task<IReadOnlyList<ToolDefinition>> GetServerToolsAsync(Guid serverId, CancellationToken ct = default)
    {
        var entry = GetEntryOrThrow(serverId);

        var tools = await entry.Client.ListToolsAsync(cancellationToken: ct);

        return tools
            .Select(t => new ToolDefinition(
                t.Name,
                t.Description ?? string.Empty,
                t.JsonSchema.ValueKind != JsonValueKind.Undefined ? t.JsonSchema.GetRawText() : null))
            .ToList();
    }

    public async Task<ToolResult> ExecuteToolAsync(Guid serverId, string toolName, string? argumentsJson, CancellationToken ct = default)
    {
        var entry = GetEntryOrThrow(serverId);

        using var timeoutCts = new CancellationTokenSource(DefaultToolTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            IReadOnlyDictionary<string, object?>? arguments = null;
            if (!string.IsNullOrWhiteSpace(argumentsJson))
            {
                arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            }

            var result = await entry.Client.CallToolAsync(toolName, arguments, cancellationToken: linkedCts.Token);

            var isError = result.IsError ?? false;
            var content = string.Join("\n", result.Content
                .OfType<TextContentBlock>()
                .Select(c => c.Text));

            return isError
                ? ToolResult.Failure(content)
                : ToolResult.Success(content);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return ToolResult.Failure($"Tool '{toolName}' execution timed out after {DefaultToolTimeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {Tool} on server {ServerId}", toolName, serverId);
            return ToolResult.Failure($"Tool execution failed: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(Guid serverId, CancellationToken ct = default)
    {
        if (!_clients.TryGetValue(serverId, out var entry))
            return false;

        try
        {
            using var timeoutCts = new CancellationTokenSource(HealthCheckTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await entry.Client.PingAsync(cancellationToken: linkedCts.Token);
            return true;
        }
        catch
        {
            _logger.LogWarning("Health check failed for MCP server {Name} ({ServerId})", entry.Config.Name, serverId);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var entry in _clients.Values)
        {
            try
            {
                await entry.Client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MCP client for server {Name}", entry.Config.Name);
            }
        }

        _clients.Clear();
    }

    private IClientTransport CreateTransport(McpServerConfig config)
    {
        return config.TransportType switch
        {
            McpTransportType.Stdio => CreateStdioTransport(config),
            McpTransportType.Sse => CreateHttpTransport(config, HttpTransportMode.Sse),
            McpTransportType.StreamableHttp => CreateHttpTransport(config, HttpTransportMode.StreamableHttp),
            _ => throw new ArgumentOutOfRangeException(nameof(config), $"Unsupported transport type: {config.TransportType}"),
        };
    }

    private StdioClientTransport CreateStdioTransport(McpServerConfig config)
    {
        var command = config.Command ?? throw new InvalidOperationException($"MCP server '{config.Name}' has no Command configured for Stdio transport");

        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(config.Arguments))
        {
            arguments.AddRange(config.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        var options = new StdioClientTransportOptions
        {
            Command = command,
            Arguments = arguments,
            Name = config.Name,
        };

        if (!string.IsNullOrWhiteSpace(config.EnvironmentVariablesJson))
        {
            var envVars = JsonSerializer.Deserialize<Dictionary<string, string>>(config.EnvironmentVariablesJson);
            if (envVars is not null)
            {
                options.EnvironmentVariables = envVars!;
            }
        }

        return new StdioClientTransport(options, _loggerFactory);
    }

    private HttpClientTransport CreateHttpTransport(McpServerConfig config, HttpTransportMode mode)
    {
        var url = config.Url ?? throw new InvalidOperationException($"MCP server '{config.Name}' has no Url configured for {config.TransportType} transport");

        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(url),
            TransportMode = mode,
            Name = config.Name,
        };

        return new HttpClientTransport(options, _loggerFactory);
    }

    private McpClientEntry GetEntryOrThrow(Guid serverId)
    {
        if (!_clients.TryGetValue(serverId, out var entry))
            throw new InvalidOperationException($"MCP server {serverId} is not connected");
        return entry;
    }
}
