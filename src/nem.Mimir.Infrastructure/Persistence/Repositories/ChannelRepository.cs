namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

internal sealed class ChannelRepository(MimirDbContext context) : IChannelRepository
{
    public async Task<Channel?> GetByIdAsync(ChannelId id, CancellationToken cancellationToken = default)
    {
        return await context.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(channel => channel.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Channel?> GetWithMembersAsync(ChannelId id, CancellationToken cancellationToken = default)
    {
        return await context.Channels
            .Include(channel => channel.Members)
            .AsSplitQuery()
            .FirstOrDefaultAsync(channel => channel.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Channel?> GetWithMessagesAsync(ChannelId id, CancellationToken cancellationToken = default)
    {
        return await context.Channels
            .Include(channel => channel.Messages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(channel => channel.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaginatedList<Channel>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Channels
            .AsNoTracking()
            .Include(channel => channel.Members)
            .Where(channel => channel.Members.Any(member => member.UserId == userId && member.LeftAt == null))
            .OrderBy(channel => channel.Name);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<Channel>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<IReadOnlyList<Channel>> GetBySourceConversationIdAsync(
        Guid sourceConversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var channels = await context.Channels
            .AsNoTracking()
            .Include(channel => channel.Members)
            .Where(channel =>
                channel.SourceConversationId == sourceConversationId &&
                channel.Members.Any(member => member.UserId == userId && member.LeftAt == null))
            .OrderByDescending(channel => channel.UpdatedAt ?? channel.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return channels;
    }

    public async Task<Channel> CreateAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        await context.Channels.AddAsync(channel, cancellationToken).ConfigureAwait(false);
        return channel;
    }

    public Task UpdateAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        context.Channels.Update(channel);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(ChannelId id, CancellationToken cancellationToken = default)
    {
        var channel = await context.Channels.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (channel is not null)
        {
            context.Channels.Remove(channel);
        }
    }

    public async Task<PaginatedList<ChannelMessage>> GetMessagesAsync(
        ChannelId channelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.ChannelMessages
            .AsNoTracking()
            .Where(message => message.ChannelId == channelId)
            .OrderBy(message => message.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<ChannelMessage>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }

    public async Task<ChannelMessage?> GetMessageByIdAsync(
        ChannelId channelId,
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChannelMessages
            .FirstOrDefaultAsync(
                message => message.ChannelId == channelId && message.Id == messageId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateMessageAsync(ChannelMessage message, CancellationToken cancellationToken = default)
    {
        context.ChannelMessages.Update(message);
        return Task.CompletedTask;
    }

    public Task DeleteMessageAsync(ChannelMessage message, CancellationToken cancellationToken = default)
    {
        context.ChannelMessages.Remove(message);
        return Task.CompletedTask;
    }
}
