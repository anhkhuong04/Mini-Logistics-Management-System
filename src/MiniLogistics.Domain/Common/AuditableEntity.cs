namespace MiniLogistics.Domain.Common;

public abstract class AuditableEntity : Entity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id)
        : base(id)
    {
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void MarkUpdated()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
