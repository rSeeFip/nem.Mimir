namespace Mimir.Domain.Common;

public abstract class BaseAuditableEntity<TId> : BaseEntity<TId>
{
    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
