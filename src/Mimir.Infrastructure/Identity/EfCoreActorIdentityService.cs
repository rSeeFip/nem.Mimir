namespace Mimir.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Mimir.Infrastructure.Persistence;
using nem.Contracts.Channels;
using nem.Contracts.Identity;

internal sealed class EfCoreActorIdentityService(MimirDbContext context) : IActorIdentityService
{
    public async Task<ActorIdentity> LinkChannelAsync(
        Guid internalUserId,
        ChannelIdentityLink link,
        CancellationToken ct = default)
    {
        var doc = await context.Set<ActorIdentityDocument>()
            .Include(a => a.Links)
            .FirstOrDefaultAsync(a => a.Id == internalUserId, ct)
            .ConfigureAwait(false);

        if (doc is null)
        {
            doc = new ActorIdentityDocument
            {
                Id = internalUserId,
                DisplayName = link.ProviderUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await context.Set<ActorIdentityDocument>()
                .AddAsync(doc, ct)
                .ConfigureAwait(false);
        }

        var existing = doc.Links.FirstOrDefault(
            l => l.ChannelType == link.ChannelType && l.ProviderUserId == link.ProviderUserId);

        if (existing is not null)
        {
            existing.TrustLevel = link.TrustLevel;
            existing.IsActive = true;
            existing.LinkedAt = link.LinkedAt;
        }
        else
        {
            doc.Links.Add(ChannelIdentityLinkDocument.FromContract(internalUserId, link));
        }

        doc.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        return doc.ToContract();
    }

    public async Task UnlinkChannelAsync(
        Guid internalUserId,
        ChannelType channelType,
        string providerUserId,
        CancellationToken ct = default)
    {
        var linkDoc = await context.Set<ChannelIdentityLinkDocument>()
            .FirstOrDefaultAsync(
                l => l.ActorIdentityId == internalUserId
                     && l.ChannelType == channelType
                     && l.ProviderUserId == providerUserId
                     && l.IsActive,
                ct)
            .ConfigureAwait(false);

        if (linkDoc is null)
            return;

        linkDoc.IsActive = false;

        var actorDoc = await context.Set<ActorIdentityDocument>()
            .FirstOrDefaultAsync(a => a.Id == internalUserId, ct)
            .ConfigureAwait(false);

        if (actorDoc is not null)
            actorDoc.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ChannelIdentityLink>> GetLinksAsync(
        Guid internalUserId,
        CancellationToken ct = default)
    {
        var links = await context.Set<ChannelIdentityLinkDocument>()
            .AsNoTracking()
            .Where(l => l.ActorIdentityId == internalUserId && l.IsActive)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return links.Select(l => l.ToContract()).ToList();
    }
}
