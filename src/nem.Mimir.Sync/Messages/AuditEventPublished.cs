namespace nem.Mimir.Sync.Messages;

/// <summary>
/// Published when an audit event should be recorded in the audit log.
/// </summary>
/// <param name="UserId">The user who performed the action.</param>
/// <param name="Action">The action that was performed.</param>
/// <param name="ResourceType">The type of resource affected.</param>
/// <param name="ResourceId">The identifier of the affected resource.</param>
/// <param name="Details">Optional additional details about the event.</param>
/// <param name="Timestamp">When the event occurred.</param>
public sealed record AuditEventPublished(
    Guid UserId,
    string Action,
    string ResourceType,
    Guid ResourceId,
    string? Details,
    DateTimeOffset Timestamp);
