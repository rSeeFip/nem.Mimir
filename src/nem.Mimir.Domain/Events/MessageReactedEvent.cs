namespace nem.Mimir.Domain.Events;

using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Domain.Common;

public sealed record MessageReactedEvent(ChannelId ChannelId, ChannelMessageId MessageId, Guid UserId, string Emoji) : IDomainEvent;
