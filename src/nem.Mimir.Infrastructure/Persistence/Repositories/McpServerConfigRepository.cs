namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.McpServers;

internal sealed class McpServerConfigRepository(MimirDbContext context) : IMcpServerConfigRepository
{
    public async Task<McpServerConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.McpServerConfigs
            .Include(c => c.ToolWhitelists)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpServerConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.McpServerConfigs
            .Include(c => c.ToolWhitelists)
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpServerConfig>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await context.McpServerConfigs
            .Include(c => c.ToolWhitelists)
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(McpServerConfig config, CancellationToken cancellationToken = default)
    {
        await context.McpServerConfigs
            .AddAsync(config, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(McpServerConfig config, CancellationToken cancellationToken = default)
    {
        context.McpServerConfigs.Update(config);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await context.McpServerConfigs
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (config is not null)
        {
            context.McpServerConfigs.Remove(config);
        }
    }
}
