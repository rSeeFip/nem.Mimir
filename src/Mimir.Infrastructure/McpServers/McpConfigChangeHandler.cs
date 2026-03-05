namespace Mimir.Infrastructure.McpServers;

using Microsoft.Extensions.Logging;
using Mimir.Domain.McpServers.Events;

/// <summary>
/// Wolverine handler for <see cref="McpConfigUpdatedEvent"/>.
/// Triggers an immediate config refresh instead of waiting for the next poll cycle.
/// </summary>
internal sealed class McpConfigChangeHandler
{
    /// <summary>
    /// Handles an MCP config update event by triggering an immediate diff/refresh.
    /// </summary>
    public static async Task Handle(
        McpConfigUpdatedEvent @event,
        McpConfigChangeListener changeListener,
        ILogger<McpConfigChangeHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Received McpConfigUpdatedEvent: ServerId={ServerId}, ChangeType={ChangeType}",
            @event.ServerId, @event.ChangeType);

        await changeListener.TriggerRefreshAsync(cancellationToken);
    }
}
