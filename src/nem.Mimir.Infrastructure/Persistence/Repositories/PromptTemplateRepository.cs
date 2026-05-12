namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

internal sealed class PromptTemplateRepository(MimirDbContext context) : IPromptTemplateRepository
{
    public async Task<PromptTemplate?> GetByIdAsync(PromptTemplateId id, CancellationToken cancellationToken = default)
    {
        return await context.PromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PromptTemplate?> GetByIdForUserAsync(PromptTemplateId id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.PromptTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PromptTemplate?> GetByCommandForUserOrSharedAsync(string command, Guid userId, CancellationToken cancellationToken = default)
    {
        var normalizedCommand = command.Trim().ToLowerInvariant();

        return await context.PromptTemplates
            .Where(t => t.Command == normalizedCommand && (t.UserId == userId || t.IsShared))
            .OrderByDescending(t => t.UserId == userId)
            .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PromptTemplate?> GetByCommandForUserAsync(string command, Guid userId, CancellationToken cancellationToken = default)
    {
        var normalizedCommand = command.Trim().ToLowerInvariant();

        return await context.PromptTemplates
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Command == normalizedCommand, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<PromptTemplate>> GetAccessibleAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        bool? isShared,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var query = context.PromptTemplates
            .AsNoTracking()
            .Where(t => t.UserId == userId || t.IsShared);

        if (isShared.HasValue)
        {
            query = query.Where(t => t.IsShared == isShared.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(t =>
                EF.Functions.ILike(t.Title, $"%{normalizedSearch}%") ||
                EF.Functions.ILike(t.Command, $"%{normalizedSearch}%") ||
                EF.Functions.ILike(t.Content, $"%{normalizedSearch}%"));
        }

        if (tags is { Count: > 0 })
        {
            var normalizedTags = tags
                .Where(static t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (normalizedTags.Length > 0)
            {
                query = query.Where(t => normalizedTags.All(tag => t.Tags.Contains(tag)));
            }
        }

        query = query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ThenBy(t => t.Title);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<PromptTemplate>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<PromptTemplate> CreateAsync(PromptTemplate template, CancellationToken cancellationToken = default)
    {
        await context.PromptTemplates
            .AddAsync(template, cancellationToken)
            .ConfigureAwait(false);

        return template;
    }

    public Task UpdateAsync(PromptTemplate template, CancellationToken cancellationToken = default)
    {
        context.PromptTemplates.Update(template);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(PromptTemplateId id, Guid userId, CancellationToken cancellationToken = default)
    {
        var promptTemplate = await context.PromptTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (promptTemplate is not null)
        {
            context.PromptTemplates.Remove(promptTemplate);
        }
    }
}
