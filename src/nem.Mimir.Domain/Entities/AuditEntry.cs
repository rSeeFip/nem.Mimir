namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class AuditEntry : BaseEntity<AuditEntryId>
{
    public UserId UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Create(
        UserId userId,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null)
    {
        if (userId.IsEmpty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty.", nameof(action));

        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type cannot be empty.", nameof(entityType));

        var auditEntry = new AuditEntry
        {
            Id = AuditEntryId.New(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = DateTimeOffset.UtcNow,
            Details = details,
            IpAddress = ipAddress
        };

        return auditEntry;
    }
}
