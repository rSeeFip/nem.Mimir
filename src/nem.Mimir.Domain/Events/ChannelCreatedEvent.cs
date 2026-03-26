namespace nem.Mimir.Domain.Events;

using ChannelId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Domain.Common;

public sealed record ChannelCreatedEvent(ChannelId ChannelId, Guid OwnerId) : IDomainEvent;
