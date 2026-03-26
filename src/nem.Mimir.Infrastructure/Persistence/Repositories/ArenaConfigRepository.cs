namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

internal sealed class ArenaConfigRepository(MimirDbContext context) : IArenaConfigRepository
{
    public async Task<ArenaConfig?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.ArenaConfigs
            .FirstOrDefaultAsync(ac => ac.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ArenaConfig> CreateAsync(ArenaConfig config, CancellationToken cancellationToken = default)
    {
        await context.ArenaConfigs
            .AddAsync(config, cancellationToken)
            .ConfigureAwait(false);

        return config;
    }

    public Task UpdateAsync(ArenaConfig config, CancellationToken cancellationToken = default)
    {
        context.ArenaConfigs.Update(config);
        return Task.CompletedTask;
    }
}
