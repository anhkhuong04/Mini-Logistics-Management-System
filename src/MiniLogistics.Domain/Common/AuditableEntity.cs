namespace MiniLogistics.Domain.Common;

/// <summary>
/// Base type for domain entities that track creation and update timestamps.
/// </summary>
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id, DateTimeOffset createdAtUtc)
        : base(id)
    {
        CreatedAtUtc = createdAtUtc;
    }

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void MarkUpdated(DateTimeOffset updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
    }
}
