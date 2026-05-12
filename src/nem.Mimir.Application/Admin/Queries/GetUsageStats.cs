using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Admin.Queries;

public sealed record GetUsageStatsQuery() : IQuery<UsageStatsDto>;

public sealed record UsageStatsDto(
    int TotalUsers,
    int TotalConversations,
    int TotalMessages,
    int ActiveUsersLast7Days,
    int ConversationsLast7Days,
    int MessagesLast7Days);

internal sealed class GetUsageStatsQueryHandler
{
    private readonly IUsageStatsReadDbContext _dbContext;

    public GetUsageStatsQueryHandler(IUsageStatsReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UsageStatsDto> Handle(GetUsageStatsQuery request, CancellationToken cancellationToken)
    {
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);

        var totalUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalConversations = await _dbContext.Conversations
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalMessages = await _dbContext.Messages
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeUsersLast7Days = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.LastLoginAt.HasValue && user.LastLoginAt.Value >= sevenDaysAgo, cancellationToken);

        var conversationsLast7Days = await _dbContext.Conversations
            .AsNoTracking()
            .CountAsync(conversation => conversation.CreatedAt >= sevenDaysAgo, cancellationToken);

        var messagesLast7Days = await _dbContext.Messages
            .AsNoTracking()
            .CountAsync(message => message.CreatedAt >= sevenDaysAgo, cancellationToken);

        return new UsageStatsDto(
            TotalUsers: totalUsers,
            TotalConversations: totalConversations,
            TotalMessages: totalMessages,
            ActiveUsersLast7Days: activeUsersLast7Days,
            ConversationsLast7Days: conversationsLast7Days,
            MessagesLast7Days: messagesLast7Days);
    }
}
