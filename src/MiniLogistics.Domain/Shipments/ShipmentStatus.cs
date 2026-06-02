namespace MiniLogistics.Domain.Shipments;

public enum ShipmentStatus
{
    Draft = 0,
    PendingPickup = 1,
    Assigned = 2,
    PickingUp = 3,
    PickedUp = 4,
    InTransit = 5,
    Delivering = 6,
    Delivered = 7,
    DeliveryFailed = 8,
    Returned = 9,
    Cancelled = 10
}
