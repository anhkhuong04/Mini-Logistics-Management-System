namespace MiniLogistics.Domain.Common;

/// <summary>
/// Base type for domain entities with a stable identifier.
/// </summary>
public abstract class Entity
{
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Entity id cannot be empty.");
        }

        Id = id;
    }

    public Guid Id { get; private set; }
}
