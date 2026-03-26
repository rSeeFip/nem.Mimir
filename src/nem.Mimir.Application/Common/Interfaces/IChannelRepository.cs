using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IChannelRepository
{
    Task<Channel?> GetByIdAsync(ChannelId id, CancellationToken cancellationToken = default);

    Task<Channel?> GetWithMembersAsync(ChannelId id, CancellationToken cancellationToken = default);

    Task<Channel?> GetWithMessagesAsync(ChannelId id, CancellationToken cancellationToken = default);

    Task<PaginatedList<Channel>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Channel>> GetBySourceConversationIdAsync(
        Guid sourceConversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Channel> CreateAsync(Channel channel, CancellationToken cancellationToken = default);

    Task UpdateAsync(Channel channel, CancellationToken cancellationToken = default);

    Task DeleteAsync(ChannelId id, CancellationToken cancellationToken = default);

    Task<PaginatedList<ChannelMessage>> GetMessagesAsync(
        ChannelId channelId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ChannelMessage?> GetMessageByIdAsync(
        ChannelId channelId,
        ChannelMessageId messageId,
        CancellationToken cancellationToken = default);

    Task UpdateMessageAsync(ChannelMessage message, CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(ChannelMessage message, CancellationToken cancellationToken = default);
}
