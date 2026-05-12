using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Application-layer MCP tool catalog that aggregates tools from all registered MCP servers
/// via <see cref="IToolProvider"/>, applies persona-based filtering, and caches results
/// with a configurable TTL and event-driven invalidation.
/// </summary>
public sealed class McpToolCatalogService : IMcpToolCatalogService
{
    private readonly IToolProvider _toolProvider;
    private readonly IPersonaToolFilter _personaFilter;
    private readonly IMemoryCache _cache;
    private readonly McpToolCatalogOptions _options;
    private readonly ILogger<McpToolCatalogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolCatalogService"/> class.
    /// </summary>
    /// <param name="toolProvider">The underlying tool provider (backed by McpClientManager via McpToolAdapter).</param>
    /// <param name="personaFilter">Persona-based tool access filter.</param>
    /// <param name="cache">Memory cache for tool catalog caching.</param>
    /// <param name="options">Catalog configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public McpToolCatalogService(
        IToolProvider toolProvider,
        IPersonaToolFilter personaFilter,
        IMemoryCache cache,
        McpToolCatalogOptions options,
        ILogger<McpToolCatalogService> logger)
    {
        ArgumentNullException.ThrowIfNull(toolProvider);
        ArgumentNullException.ThrowIfNull(personaFilter);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _toolProvider = toolProvider;
        _personaFilter = personaFilter;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(
        string? persona = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var allTools = await GetCachedToolsAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(persona))
        {
            return allTools;
        }

        return await _personaFilter.FilterAsync(allTools, persona, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ToolInvocationResult> InvokeToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("Routing tool invocation for '{ToolName}' to source MCP server.", toolName);

        // Delegate to the underlying IToolProvider which routes to the correct MCP server
        // via McpToolAdapter → McpClientManager.
        return await _toolProvider.InvokeToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolDefinition>> SearchToolsAsync(
        string query,
        string? persona = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        var tools = await ListToolsAsync(persona, cancellationToken).ConfigureAwait(false);

        return tools
            .Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolDefinition>> ListToolsByServerAsync(
        string serverName,
        string? persona = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        cancellationToken.ThrowIfCancellationRequested();

        var tools = await ListToolsAsync(persona, cancellationToken).ConfigureAwait(false);

        return tools
            .Where(t => string.Equals(t.ServerName, serverName, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        _cache.Remove(_options.CacheKey);
        _logger.LogInformation("MCP tool catalog cache invalidated.");
    }

    /// <summary>
    /// Retrieves all tools from cache, or fetches from the underlying provider and caches the result.
    /// </summary>
    private async Task<IReadOnlyList<ToolDefinition>> GetCachedToolsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(_options.CacheKey, out IReadOnlyList<ToolDefinition>? cached) && cached is not null)
        {
            _logger.LogDebug("Returning {Count} tools from cache.", cached.Count);
            return cached;
        }

        _logger.LogDebug("Cache miss — aggregating tools from all MCP servers.");

        var tools = await _toolProvider.ListToolsAsync(cancellationToken).ConfigureAwait(false);

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.CacheTtl
        };

        _cache.Set(_options.CacheKey, tools, cacheOptions);

        _logger.LogInformation("Cached {Count} tools from MCP servers (TTL={Ttl}).", tools.Count, _options.CacheTtl);

        return tools;
    }
}
