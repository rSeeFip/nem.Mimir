using nem.Mimir.Domain.Common;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Repository interface for restoring soft-deleted entities.
/// Uses IgnoreQueryFilters to access soft-deleted records.
/// </summary>
public interface IEntityRestoreRepository
{
    /// <summary>
    /// Finds a soft-deleted entity by type and identifier, ignoring query filters.
    /// </summary>
    /// <param name="entityType">The entity type name (e.g., "conversation", "user", "systemprompt").</param>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found (including soft-deleted); otherwise, null.</returns>
    Task<object?> GetByIdIncludingDeletedAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted entity by setting IsDeleted to false and DeletedAt to null.
    /// </summary>
    /// <param name="entity">The entity to restore.</param>
    void Restore(object entity);
}
