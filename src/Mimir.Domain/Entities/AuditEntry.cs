namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;

public class AuditEntry : BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Create(
        Guid userId,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty.", nameof(action));

        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type cannot be empty.", nameof(entityType));

        var auditEntry = new AuditEntry
        {
            Id = Guid.NewGuid(),
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
