namespace Mimir.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Enums;
using Mimir.Infrastructure.Persistence;

internal sealed class ConversationArchiveService(
    MimirDbContext context,
    IDateTimeService dateTimeService,
    IUnitOfWork unitOfWork) : IConversationArchiveService
{
    public async Task<int> ArchiveInactiveConversationsAsync(int inactiveDays, CancellationToken cancellationToken = default)
    {
        if (inactiveDays <= 0)
            return 0;

        var cutoffDate = dateTimeService.UtcNow.AddDays(-inactiveDays);

        var staleConversations = await context.Conversations
            .Where(c => c.Status == ConversationStatus.Active)
            .Where(c => (c.UpdatedAt ?? c.CreatedAt) < cutoffDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var conversation in staleConversations)
        {
            conversation.Archive();
        }

        if (staleConversations.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return staleConversations.Count;
    }
}
