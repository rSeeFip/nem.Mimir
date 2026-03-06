namespace Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;

internal sealed class ChannelEventRepository(MimirDbContext context) : IChannelEventRepository
{
    public async Task<ChannelEvent> CreateAsync(ChannelEvent channelEvent, CancellationToken cancellationToken = default)
    {
        await context.ChannelEvents
            .AddAsync(channelEvent, cancellationToken)
            .ConfigureAwait(false);

        return channelEvent;
    }

    public async Task<ChannelEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ChannelEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
