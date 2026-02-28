namespace Mimir.Application.Common.Interfaces;

/// <summary>
/// Represents a unit of work for coordinating transactional persistence operations.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all pending changes to the underlying data store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the data store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
