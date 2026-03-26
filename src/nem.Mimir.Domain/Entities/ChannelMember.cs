namespace nem.Mimir.Domain.Entities;

using ChannelId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public sealed class ChannelMember : BaseAuditableEntity<Guid>
{
    public ChannelId ChannelId { get; private set; }
    public Guid UserId { get; private set; }
    public ChannelMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }

    private ChannelMember() { }

    public static ChannelMember Create(ChannelId channelId, Guid userId, ChannelMemberRole role)
    {
        if (channelId.IsEmpty)
            throw new ArgumentException("Channel ID cannot be empty.", nameof(channelId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        return new ChannelMember
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        };
    }

    public void ChangeRole(ChannelMemberRole role)
    {
        Role = role;
    }

    public void Leave()
    {
        LeftAt = DateTimeOffset.UtcNow;
    }
}
