using Microsoft.Extensions.Logging;

namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Wolverine message handler that invalidates the MCP tool catalog cache
/// when a <see cref="McpToolCatalogCacheInvalidatedEvent"/> is received.
/// </summary>
public sealed class McpToolCatalogCacheInvalidatedHandler
{
    private readonly IMcpToolCatalogService _catalogService;
    private readonly ILogger<McpToolCatalogCacheInvalidatedHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolCatalogCacheInvalidatedHandler"/> class.
    /// </summary>
    public McpToolCatalogCacheInvalidatedHandler(
        IMcpToolCatalogService catalogService,
        ILogger<McpToolCatalogCacheInvalidatedHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(catalogService);
        ArgumentNullException.ThrowIfNull(logger);

        _catalogService = catalogService;
        _logger = logger;
    }

    /// <summary>
    /// Handles cache invalidation events by delegating to the catalog service.
    /// Wolverine discovers this handler by convention (Handle method on a handler class).
    /// </summary>
    public void Handle(McpToolCatalogCacheInvalidatedEvent @event)
    {
        _logger.LogInformation(
            "Received cache invalidation event. Reason: {Reason}",
            @event.Reason ?? "not specified");

        _catalogService.InvalidateCache();
    }
}
