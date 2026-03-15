using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Repository interface for User entity persistence operations.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by their unique identifier.
    /// </summary>
    /// <param name="id">The user's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);


    /// <summary>
    /// Creates a new user in the repository.
    /// </summary>
    /// <param name="user">The user entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user entity.</returns>
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user in the repository.
    /// </summary>
    /// <param name="user">The user entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of all users.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of users.</returns>
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
