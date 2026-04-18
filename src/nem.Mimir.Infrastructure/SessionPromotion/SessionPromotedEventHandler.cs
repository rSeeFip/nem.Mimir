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
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var oldKey = $"session:{message.ConversationForkId}:{message.OldChannelId}";
        var newKey = $"session:{message.ConversationForkId}:{message.NewChannelId}";

        await cache.InvalidateAsync(oldKey, cancellationToken);

        await cache.SetAsync(newKey, message.OldChannelId.ToString(), cancellationToken: cancellationToken);

        logger.LogInformation(
            "Session promoted for conversation fork {ConversationForkId} from channel {OldChannelId} to {NewChannelId}. Namespaced cache key {OldKey} invalidated, {NewKey} seeded.",
            message.ConversationForkId,
            message.OldChannelId,
            message.NewChannelId,
            oldKey,
            newKey);
    }
}
