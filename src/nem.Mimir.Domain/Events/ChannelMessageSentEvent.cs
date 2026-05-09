namespace nem.Mimir.Domain.Events;

using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Domain.Common;

public sealed record ChannelMessageSentEvent(ChannelMessageId MessageId, ChannelId ChannelId, Guid SenderId) : IDomainEvent;
