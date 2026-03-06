namespace Mimir.Infrastructure.Identity;

using nem.Contracts.Channels;
using nem.Contracts.Identity;

/// <summary>
/// EF Core entity for persisting <see cref="ActorIdentity"/>.
/// </summary>
public sealed class ActorIdentityDocument
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ChannelIdentityLinkDocument> Links { get; set; } = [];

    internal ActorIdentityDocument() { }

    public static ActorIdentityDocument FromContract(ActorIdentity identity) => new()
    {
        Id = identity.InternalUserId,
        DisplayName = identity.DisplayName,
        Email = identity.Email,
        CreatedAt = identity.CreatedAt,
        UpdatedAt = identity.UpdatedAt,
        Links = identity.Links.Select(l => ChannelIdentityLinkDocument.FromContract(identity.InternalUserId, l)).ToList(),
    };

    public ActorIdentity ToContract() => new()
    {
        InternalUserId = Id,
        DisplayName = DisplayName,
        Email = Email,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Links = Links.Where(l => l.IsActive).Select(l => l.ToContract()).ToList(),
    };
}
