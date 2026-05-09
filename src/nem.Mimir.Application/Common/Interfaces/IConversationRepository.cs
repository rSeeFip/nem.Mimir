using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Conversation entity persistence operations.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Gets a conversation by its unique identifier.
    /// </summary>
    /// <param name="id">The conversation's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation if found; otherwise, null.</returns>
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of conversations for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of conversations.</returns>
    Task<PaginatedList<Conversation>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Conversation>> GetAllByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new conversation in the repository.
    /// </summary>
    /// <param name="conversation">The conversation entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation entity.</returns>
    Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing conversation in the repository.
    /// </summary>
    /// <param name="conversation">The conversation entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a conversation with all its messages eagerly loaded.
    /// </summary>
    /// <param name="id">The conversation's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation with messages if found; otherwise, null.</returns>
    Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a conversation from the repository.
    /// </summary>
    /// <param name="id">The conversation's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of messages for a specific conversation.
    /// </summary>
    /// <param name="conversationId">The conversation's unique identifier.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of messages.</returns>
    Task<PaginatedList<Message>> GetMessagesAsync(
        Guid conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
