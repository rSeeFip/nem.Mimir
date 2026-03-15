namespace nem.Mimir.Infrastructure.Mcp;

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.Tools;

public sealed class McpClientManager
{
    internal const string HttpClientName = "McpClientManager";
    private const int MaxRetries = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<McpClientManager> _logger;
    private readonly ConcurrentDictionary<string, ServerState> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly List<McpServerConfiguration> _configuredServers;
    private bool _initialized;

    public McpClientManager(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider,
        ILogger<McpClientManager> logger)
        : this(
            httpClientFactory,
            configuration.GetSection("McpServers").Get<List<McpServerConfiguration>>() ?? [],
            httpContextAccessor,
            timeProvider,
            logger)
    {
    }

    internal McpClientManager(
        IHttpClientFactory httpClientFactory,
        IEnumerable<McpServerConfiguration> configurations,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider,
        ILogger<McpClientManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
        _logger = logger;
        _configuredServers = configurations.ToList();
    }

    public async Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var tools = new List<ToolDefinition>();
        foreach (var server in _servers.Values)
        {
            var discovered = await GetOrRefreshToolsAsync(server, cancellationToken);
            tools.AddRange(discovered);
        }

        return tools;
    }

    public async Task<ToolInvocationResult> InvokeToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(toolName))
        {
            return new ToolInvocationResult(false, null, "Tool name is required.");
        }

        var route = await ResolveRouteAsync(toolName, cancellationToken);
        if (route is null)
        {
            return new ToolInvocationResult(false, null, $"Tool '{toolName}' not found in configured MCP servers.");
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = route.Value.LocalToolName,
            ["arguments"] = arguments,
        };

        Exception? lastError = null;
        var delay = TimeSpan.FromMilliseconds(200);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var client = CreateClient(route.Value.Server.Configuration);
                using var response = await client.PostAsJsonAsync("/tools/call", payload, JsonOptions, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new ToolInvocationResult(
                        false,
                        null,
                        BuildHttpErrorMessage(route.Value.Server.Configuration.Name, response.StatusCode, body));
                }

                var result = await response.Content.ReadFromJsonAsync<ToolCallHttpResponse>(JsonOptions, cancellationToken);
                if (result is null)
                {
                    return new ToolInvocationResult(false, null, "Empty response from MCP server.");
                }

                var content = ExtractContent(result);
                var error = ExtractError(result, content);
                var success = !result.IsError && string.IsNullOrWhiteSpace(error);

                return success
                    ? new ToolInvocationResult(true, content, null)
                    : new ToolInvocationResult(false, null, error ?? "MCP tool invocation failed.");
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                lastError = ex;
                _logger.LogWarning(ex, "MCP tool call failed ({Attempt}/{Max}) for {ToolName}", attempt, MaxRetries, toolName);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        return new ToolInvocationResult(false, null, lastError?.Message ?? "MCP tool invocation failed.");
    }

    public Task RegisterServerAsync(McpServerConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(configuration);
        _servers[configuration.Name] = new ServerState(configuration);
        return Task.CompletedTask;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            foreach (var server in _configuredServers)
            {
                await RegisterServerAsync(server, cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<(ServerState Server, string LocalToolName)?> ResolveRouteAsync(
        string toolName,
        CancellationToken cancellationToken)
    {
        var prefixRoute = TryResolveByPrefix(toolName);
        if (prefixRoute is not null)
        {
            return prefixRoute;
        }

        foreach (var server in _servers.Values)
        {
            var tools = await GetOrRefreshToolsAsync(server, cancellationToken);
            if (tools.Any(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase)))
            {
                return (server, toolName);
            }
        }

        return null;
    }

    private (ServerState Server, string LocalToolName)? TryResolveByPrefix(string toolName)
    {
        var separatorIndex = toolName.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var serverName = toolName[..separatorIndex];
        var localToolName = toolName[(separatorIndex + 1)..];
        if (string.IsNullOrWhiteSpace(localToolName))
        {
            return null;
        }

        return _servers.TryGetValue(serverName, out var server)
            ? (server, localToolName)
            : null;
    }

    private async Task<IReadOnlyList<ToolDefinition>> GetOrRefreshToolsAsync(
        ServerState server,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (server.CachedTools is not null && now < server.CacheExpiresAt)
        {
            return server.CachedTools;
        }

        await server.CacheGate.WaitAsync(cancellationToken);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (server.CachedTools is not null && now < server.CacheExpiresAt)
            {
                return server.CachedTools;
            }

            using var client = CreateClient(server.Configuration);
            using var response = await client.GetAsync("/tools/list", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed discovering tools from {Server}. Status={StatusCode}, Body={Body}",
                    server.Configuration.Name,
                    response.StatusCode,
                    body);

                return server.CachedTools ?? [];
            }

            var listResponse = await response.Content.ReadFromJsonAsync<ToolsListHttpResponse>(JsonOptions, cancellationToken)
                ?? new ToolsListHttpResponse();

            var mapped = listResponse.Tools
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => new ToolDefinition(
                    t.Name,
                    t.Description ?? string.Empty,
                    server.Configuration.Name,
                    BuildSchemaDocument(t.InputSchema)))
                .ToList();

            server.CachedTools = mapped;
            server.CacheExpiresAt = now.Add(GetCacheTtl(server.Configuration));
            return mapped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering tools for MCP server {Server}", server.Configuration.Name);
            return server.CachedTools ?? [];
        }
        finally
        {
            server.CacheGate.Release();
        }
    }

    private HttpClient CreateClient(McpServerConfiguration configuration)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(configuration.Url, UriKind.Absolute);
        client.Timeout = configuration.ConnectionTimeout > TimeSpan.Zero
            ? configuration.ConnectionTimeout
            : TimeSpan.FromSeconds(30);

        if (configuration.RequiresAuth)
        {
            var token = ResolveBearerToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnauthorizedAccessException($"MCP server '{configuration.Name}' requires JWT auth.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private string? ResolveBearerToken()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    private static string BuildHttpErrorMessage(string serverName, System.Net.HttpStatusCode statusCode, string body)
    {
        var compact = string.IsNullOrWhiteSpace(body) ? string.Empty : $" Body: {body.Trim()}";
        return $"MCP server '{serverName}' returned {(int)statusCode} ({statusCode}).{compact}";
    }

    private static TimeSpan GetCacheTtl(McpServerConfiguration configuration)
        => configuration.ToolSchemasCacheTtl > TimeSpan.Zero
            ? configuration.ToolSchemasCacheTtl
            : TimeSpan.FromMinutes(5);

    private static JsonDocument? BuildSchemaDocument(JsonElement? inputSchema)
        => inputSchema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null }
            ? JsonDocument.Parse(inputSchema.Value.GetRawText())
            : null;

    private static string? ExtractContent(ToolCallHttpResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Content))
        {
            return response.Content;
        }

        if (response.Result.HasValue)
        {
            return response.Result.Value.ValueKind switch
            {
                JsonValueKind.String => response.Result.Value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => response.Result.Value.GetRawText(),
            };
        }

        if (response.Output is { Count: > 0 })
        {
            var builder = new StringBuilder();
            foreach (var item in response.Output)
            {
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append(item.Text);
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        return null;
    }

    private static string? ExtractError(ToolCallHttpResponse response, string? content)
    {
        if (!string.IsNullOrWhiteSpace(response.Error))
        {
            return response.Error;
        }

        return response.IsError ? content ?? "MCP tool invocation failed." : null;
    }

    private static void ValidateConfiguration(McpServerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Url);
    }

    private sealed class ServerState(McpServerConfiguration configuration)
    {
        public McpServerConfiguration Configuration { get; } = configuration;

        public SemaphoreSlim CacheGate { get; } = new(1, 1);

        public IReadOnlyList<ToolDefinition>? CachedTools { get; set; }

        public DateTimeOffset CacheExpiresAt { get; set; }
    }

    private sealed class ToolsListHttpResponse
    {
        public List<ToolListItem> Tools { get; set; } = [];
    }

    private sealed class ToolListItem
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public JsonElement? InputSchema { get; set; }
    }

    private sealed class ToolCallHttpResponse
    {
        public bool IsError { get; set; }

        public string? Content { get; set; }

        public string? Error { get; set; }

        public JsonElement? Result { get; set; }

        public List<ToolOutputItem>? Output { get; set; }
    }

    private sealed class ToolOutputItem
    {
        public string? Text { get; set; }
    }
}
