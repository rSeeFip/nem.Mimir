using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Api.Hubs;

public interface ICollaborationHubClient
{
    Task ChannelMessage(ChannelMessageDto message);

    Task UserTyping(object payload);

    Task UserStoppedTyping(object payload);

    Task TypingIndicator(object payload);

    Task DocumentState(string documentId, byte[] state);

    Task DocumentSync(string documentId, byte[] syncMessage);

    Task DocumentPresenceChanged(object payload);

    Task AwarenessUpdate(object payload);

    Task UserCursorMoved(DocumentCursorDto payload);

    Task UserCursorLeft(DocumentCursorLeftDto payload);

    Task PresenceUpdated(object payload);
}
