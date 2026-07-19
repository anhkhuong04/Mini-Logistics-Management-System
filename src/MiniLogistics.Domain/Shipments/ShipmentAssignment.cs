using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

/// <summary>
/// Represents the Shipment Assignment domain entity.
/// </summary>
public sealed class ShipmentAssignment : Entity
{
    private ShipmentAssignment()
    {
    }

    internal ShipmentAssignment(Guid shipmentId, Guid shipperId, DateTimeOffset assignedAtUtc)
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
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid ShipmentId { get; private set; }

    public Guid ShipperId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public DateTimeOffset? UnassignedAtUtc { get; private set; }

    public bool IsActive => UnassignedAtUtc is null;

    internal void Deactivate(DateTimeOffset unassignedAtUtc)
    {
        if (UnassignedAtUtc is not null)
        {
            return;
        }

        UnassignedAtUtc = unassignedAtUtc;
    }
}
