namespace nem.Mimir.Domain.Common;

public abstract class BaseEntity<TId>
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity<TId> other)
            return false;

        return Id!.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return Id!.GetHashCode();
    }

    public static bool operator ==(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        return !(left == right);
    }
}
