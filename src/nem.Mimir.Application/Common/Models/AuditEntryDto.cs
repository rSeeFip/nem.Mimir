namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Data transfer object representing an audit log entry.
/// </summary>
/// <param name="Id">The audit entry's unique identifier.</param>
/// <param name="UserId">The identifier of the user who performed the action.</param>
/// <param name="Action">The action that was performed.</param>
/// <param name="EntityType">The type of entity affected.</param>
/// <param name="EntityId">The identifier of the entity affected, if applicable.</param>
/// <param name="Timestamp">The timestamp when the action occurred.</param>
/// <param name="Details">Additional details about the action, if any.</param>
/// <param name="IpAddress">The IP address from which the action was performed, if available.</param>
public sealed record AuditEntryDto(
    Guid Id,
    Guid UserId,
    string Action,
    string EntityType,
    string? EntityId,
    DateTimeOffset Timestamp,
    string? Details,
    string? IpAddress);
