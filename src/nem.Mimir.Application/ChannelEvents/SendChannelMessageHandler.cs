using Microsoft.Extensions.Logging;

namespace nem.Mimir.Application.ChannelEvents;

/// <summary>
/// Handles <see cref="SendChannelMessageCommand"/> by routing the outbound message
/// to the appropriate channel adapter via <see cref="ChannelEventRouter"/>.
/// </summary>
internal sealed class SendChannelMessageHandler(
    ChannelEventRouter router,
    ILogger<SendChannelMessageHandler> logger)
{
    public async Task<SendChannelMessageResult> Handle(SendChannelMessageCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Sending message to {Channel} channel {ExternalChannelId}",
            request.Channel,
            request.ExternalChannelId);

        var sender = router.GetSender(request.Channel);
        return await sender.SendAsync(request.ExternalChannelId, request.Content, cancellationToken);
    }
}
