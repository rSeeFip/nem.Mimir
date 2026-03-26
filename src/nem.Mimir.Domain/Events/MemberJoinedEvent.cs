namespace nem.Mimir.Domain.Events;

using ChannelId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Domain.Common;

public sealed record MemberJoinedEvent(ChannelId ChannelId, Guid UserId) : IDomainEvent;
