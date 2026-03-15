namespace nem.Mimir.Domain.Common;

public abstract class BaseAuditableEntity<TId> : BaseEntity<TId>
{
    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }
}
