using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

public sealed class ShipmentAssignment : Entity
{
    private ShipmentAssignment()
    {
    }

    internal ShipmentAssignment(Guid shipmentId, Guid shipperId)
        : base(Guid.NewGuid())
    {
        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Shipment id is required.");
        }

        if (shipperId == Guid.Empty)
        {
            throw new DomainException(ShipmentErrors.InvalidShipper);
        }

        ShipmentId = shipmentId;
        ShipperId = shipperId;
        AssignedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid ShipmentId { get; private set; }

    public Guid ShipperId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public DateTimeOffset? UnassignedAtUtc { get; private set; }

    public bool IsActive => UnassignedAtUtc is null;

    internal void Deactivate()
    {
        if (UnassignedAtUtc is not null)
        {
            return;
        }

        UnassignedAtUtc = DateTimeOffset.UtcNow;
    }
}
