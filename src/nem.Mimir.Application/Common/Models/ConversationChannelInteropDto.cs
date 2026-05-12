namespace nem.Mimir.Application.Common.Models;

public sealed record ConvertConversationToChannelDto(
    Guid ConversationId,
    Guid ChannelId,
    string ChannelName);

public sealed record ShareConversationToChannelDto(
    Guid ConversationId,
    Guid ChannelId,
    Guid SharedMessageId,
    bool IsMessageShare);
