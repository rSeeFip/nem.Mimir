using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Repository interface for AuditEntry entity persistence operations.
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Creates a new audit entry in the repository.
    /// </summary>
    /// <param name="auditEntry">The audit entry entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created audit entry entity.</returns>
    Task<AuditEntry> CreateAsync(AuditEntry auditEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of audit entries for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of audit entries.</returns>
    Task<PaginatedList<AuditEntry>> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of audit entries for a specific entity.
    /// </summary>
    /// <param name="entityType">The type name of the entity.</param>
    /// <param name="entityId">The entity's unique identifier.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of audit entries for the specified entity.</returns>
    Task<PaginatedList<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

 /// <summary>
 /// Gets a paginated list of all audit entries.
 /// </summary>
 /// <param name="pageNumber">The page number (1-based).</param>
 /// <param name="pageSize">The number of items per page.</param>
 /// <param name="cancellationToken">Cancellation token.</param>
 /// <returns>A paginated list of all audit entries.</returns>
 Task<PaginatedList<AuditEntry>> GetAllAsync(
 int pageNumber,
 int pageSize,
 CancellationToken cancellationToken = default);
}
