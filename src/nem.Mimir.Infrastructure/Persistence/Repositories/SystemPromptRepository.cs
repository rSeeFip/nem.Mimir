namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

internal sealed class SystemPromptRepository(MimirDbContext context) : ISystemPromptRepository
{
    public async Task<SystemPrompt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.SystemPrompts
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<SystemPrompt>> GetAllAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.SystemPrompts
            .AsNoTracking()
            .Where(sp => sp.IsActive)
            .OrderByDescending(sp => sp.CreatedAt);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<SystemPrompt>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<SystemPrompt> CreateAsync(SystemPrompt systemPrompt, CancellationToken cancellationToken = default)
    {
        await context.SystemPrompts
            .AddAsync(systemPrompt, cancellationToken)
            .ConfigureAwait(false);

        return systemPrompt;
    }

    public Task UpdateAsync(SystemPrompt systemPrompt, CancellationToken cancellationToken = default)
    {
        context.SystemPrompts.Update(systemPrompt);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var prompt = await context.SystemPrompts
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (prompt is not null)
        {
            // Explicit soft-delete instead of Remove() to avoid reliance on interceptor
            prompt.Deactivate();
            context.Entry(prompt).Property("IsDeleted").CurrentValue = true;
            context.Entry(prompt).Property("DeletedAt").CurrentValue = DateTimeOffset.UtcNow;
        }
    }

    public async Task<SystemPrompt?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await context.SystemPrompts
            .AsNoTracking()
            .Where(sp => sp.IsDefault && sp.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
