using Mimir.Domain.ValueObjects;

namespace Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for creating audit log entries.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Creates an audit log entry for the specified action.
    /// </summary>
    /// <param name="userId">The identifier of the user who performed the action.</param>
    /// <param name="action">The action that was performed.</param>
    /// <param name="entityType">The type of entity affected.</param>
    /// <param name="entityId">The identifier of the entity affected, if applicable.</param>
    /// <param name="details">Additional details about the action, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(
        UserId userId,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
