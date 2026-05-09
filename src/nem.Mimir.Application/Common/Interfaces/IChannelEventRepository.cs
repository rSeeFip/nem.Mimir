using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Repository interface for ChannelEvent entity persistence operations.
/// </summary>
public interface IChannelEventRepository
{
    /// <summary>
    /// Persists a new channel event.
    /// </summary>
    /// <param name="channelEvent">The channel event entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created channel event entity.</returns>
    Task<ChannelEvent> CreateAsync(ChannelEvent channelEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a channel event by its unique identifier.
    /// </summary>
    /// <param name="id">The channel event's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The channel event if found; otherwise, null.</returns>
    Task<ChannelEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
