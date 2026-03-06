namespace Mimir.Infrastructure.Identity;

using nem.Contracts.Channels;
using nem.Contracts.Identity;

/// <summary>
/// EF Core entity for persisting <see cref="ChannelIdentityLink"/>.
/// </summary>
public sealed class ChannelIdentityLinkDocument
{
    public Guid Id { get; set; }
    public Guid ActorIdentityId { get; set; }
    public ChannelType ChannelType { get; set; }
    public string ProviderUserId { get; set; } = string.Empty;
    public TrustLevel TrustLevel { get; set; }
    public DateTime LinkedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ActorIdentityDocument? ActorIdentity { get; set; }

    public ChannelIdentityLinkDocument() { }

    public static ChannelIdentityLinkDocument FromContract(Guid actorIdentityId, ChannelIdentityLink link) => new()
    {
        Id = Guid.NewGuid(),
        ActorIdentityId = actorIdentityId,
        ChannelType = link.ChannelType,
        ProviderUserId = link.ProviderUserId,
        TrustLevel = link.TrustLevel,
        LinkedAt = link.LinkedAt,
        IsActive = link.IsActive,
    };

    public ChannelIdentityLink ToContract() => new()
    {
        ChannelType = ChannelType,
        ProviderUserId = ProviderUserId,
        TrustLevel = TrustLevel,
        LinkedAt = LinkedAt,
        IsActive = IsActive,
    };
}
