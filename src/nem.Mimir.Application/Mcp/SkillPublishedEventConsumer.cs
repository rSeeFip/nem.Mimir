using Microsoft.Extensions.Logging;
using nem.Contracts.CloudEvents;
using nem.Contracts.Events.Integration;

namespace nem.Mimir.Application.Mcp;

public static class SkillPublishedEventConsumer
{
    public static Task Handle(
        NemCloudEvent<SkillPublishedEvent> @event,
        IMcpToolCatalogService catalogService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ct.ThrowIfCancellationRequested();

        var logger = loggerFactory.CreateLogger("nem.Mimir.Application.Mcp.SkillPublishedEventConsumer");
        catalogService.InvalidateCache();

        logger.LogInformation(
            "Invalidated MCP tool catalog cache due to published skill {SkillId}/{SkillVersionId}",
            @event.Data.SkillId,
            @event.Data.SkillVersionId);

        return Task.CompletedTask;
    }
}
