namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

internal sealed class ConversationRepository(MimirDbContext context) : IConversationRepository
{
    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<Conversation>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<Conversation>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await context.Conversations
            .AddAsync(conversation, cancellationToken)
            .ConfigureAwait(false);

        return conversation;
    }

    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        context.Conversations.Update(conversation);
        return Task.CompletedTask;
    }

    public async Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .Include(c => c.Messages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await context.Conversations
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (conversation is not null)
        {
            context.Conversations.Remove(conversation);
        }
    }

    public async Task<PaginatedList<Message>> GetMessagesAsync(
        Guid conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<Message>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }
}
