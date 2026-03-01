namespace Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for auto-archiving conversations based on age thresholds.
/// </summary>
public interface IConversationArchiveService
{
    /// <summary>
    /// Archives all active conversations that have been inactive for longer than the specified number of days.
    /// </summary>
    /// <param name="inactiveDays">Number of days of inactivity after which a conversation should be archived.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of conversations that were archived.</returns>
    Task<int> ArchiveInactiveConversationsAsync(int inactiveDays, CancellationToken cancellationToken = default);
}
