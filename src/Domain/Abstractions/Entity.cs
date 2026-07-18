namespace Domain.Abstractions;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public IReadOnlyCollection<IDomainEvent> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}

public abstract class AuditableEntity<TId> : AuditableEntity
{
    protected AuditableEntity(TId id) { Id = id; }

    public TId Id { get; protected set; }
}

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public void MarkCreated(DateTime at) => CreatedAt = at;
    public void MarkUpdated(DateTime at) => UpdatedAt = at;

    public void MarkDeleted(DateTime at)
    {
        IsDeleted = true;
        DeletedAt = at;
        UpdatedAt = at;
    }
}
