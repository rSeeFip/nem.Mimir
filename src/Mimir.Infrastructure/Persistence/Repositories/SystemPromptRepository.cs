namespace Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using Mimir.Domain.ValueObjects;

internal sealed class SystemPromptRepository(MimirDbContext context) : ISystemPromptRepository
{
    public async Task<SystemPrompt?> GetByIdAsync(SystemPromptId id, CancellationToken cancellationToken = default)
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

    public async Task DeleteAsync(SystemPromptId id, CancellationToken cancellationToken = default)
    {
        var prompt = await context.SystemPrompts
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (prompt is not null)
        {
            context.SystemPrompts.Remove(prompt);
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
