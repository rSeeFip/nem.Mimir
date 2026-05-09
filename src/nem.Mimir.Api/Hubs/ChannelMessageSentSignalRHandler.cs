using Microsoft.AspNetCore.SignalR;
using nem.Mimir.Domain.Events;

namespace nem.Mimir.Api.Hubs;

public sealed class ChannelMessageSentSignalRHandler
{
    private readonly IHubContext<CollaborationHub> _hubContext;

    public ChannelMessageSentSignalRHandler(IHubContext<CollaborationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Handle(ChannelMessageSentEvent notification, CancellationToken cancellationToken)
    {
        var groupName = $"channel:{notification.ChannelId.Value}";

        await _hubContext.Clients.Group(groupName).SendAsync("ChannelMessageSent", new
        {
            channelId = notification.ChannelId.Value,
            messageId = notification.MessageId.Value,
            senderId = notification.SenderId,
            timestamp = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }
}
