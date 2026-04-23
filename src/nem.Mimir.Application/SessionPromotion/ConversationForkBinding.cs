using nem.Contracts.Identity;

namespace nem.Mimir.Application.SessionPromotion;

public sealed record ConversationForkBinding(
    ConversationForkId ForkId,
    ChannelId OldChannelId,
    ChannelId NewChannelId,
    string AdapterName,
    DateTimeOffset PromotedAtUtc)
{
    public Guid Id => ForkId.Value;
}
