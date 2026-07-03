using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

public static class ShipmentLoadStatuses
{
    public static readonly ShipmentStatus[] ActiveAssignmentStatuses =
    [
        ShipmentStatus.Assigned,
        ShipmentStatus.PickingUp,
        ShipmentStatus.PickedUp,
        ShipmentStatus.InTransit,
        ShipmentStatus.Delivering,
        ShipmentStatus.DeliveryFailed
    ];
}
