namespace Mimir.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Mimir.Infrastructure.Persistence;
using nem.Contracts.Channels;
using nem.Contracts.Identity;

internal sealed class EfCoreActorIdentityResolver(MimirDbContext context) : IActorIdentityResolver
{
    public async Task<ActorIdentity?> ResolveAsync(
        ChannelType channelType,
        string providerUserId,
        CancellationToken ct = default)
    {
        var doc = await context.Set<ActorIdentityDocument>()
            .AsNoTracking()
            .Include(a => a.Links)
            .FirstOrDefaultAsync(
                a => a.Links.Any(l => l.ChannelType == channelType
                                      && l.ProviderUserId == providerUserId
                                      && l.IsActive),
                ct)
            .ConfigureAwait(false);

        return doc?.ToContract();
    }
}
