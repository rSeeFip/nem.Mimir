namespace nem.Mimir.Infrastructure.SessionPromotion;

using Microsoft.Extensions.Logging;
using nem.Contracts.SessionPromotion;
using nem.Contracts.TokenOptimization;
using Wolverine.Attributes;

public static class SessionPromotedEventHandler
{
    [WolverineHandler]
    public static async Task HandleAsync(
        SessionPromotedEvent message,
        ISemanticCache cache,
        ILogger<SessionPromotedEventHandler> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var oldKey = $"session:{message.OldChannelId}";
        var newKey = $"session:{message.NewChannelId}";

        await cache.InvalidateAsync(oldKey, cancellationToken);

        await cache.SetAsync(newKey, message.OldChannelId.ToString(), cancellationToken: cancellationToken);

        logger.LogInformation(
            "Session promoted from channel {OldChannelId} to {NewChannelId}. Cache key {OldKey} invalidated, {NewKey} seeded.",
            message.OldChannelId,
            message.NewChannelId,
            oldKey,
            newKey);
    }
}
