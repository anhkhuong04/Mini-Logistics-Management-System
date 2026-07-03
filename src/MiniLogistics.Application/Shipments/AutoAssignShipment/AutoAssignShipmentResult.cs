using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.AutoAssignShipment;

public enum AutoAssignShipmentStatus
{
    Assigned = 1,
    NoEligibleShipper = 2,
    Skipped = 3
}

public sealed record AutoAssignShipmentResult(
    Guid ShipmentId,
    string TrackingCode,
    ShipmentStatus ShipmentStatus,
    AutoAssignShipmentStatus Status,
    Guid? ShipperId,
    string Reason)
{
    public static AutoAssignShipmentResult Assigned(
        Shipment shipment,
        Guid shipperId,
        string reason)
    {
        return new AutoAssignShipmentResult(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.Status,
            AutoAssignShipmentStatus.Assigned,
            shipperId,
            reason);
    }

    public static AutoAssignShipmentResult NoEligibleShipper(
        Shipment shipment,
        string reason)
    {
        return new AutoAssignShipmentResult(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.Status,
            AutoAssignShipmentStatus.NoEligibleShipper,
            null,
            reason);
    }

    public static AutoAssignShipmentResult Skipped(
        Shipment shipment,
        string reason)
    {
        return new AutoAssignShipmentResult(
            shipment.Id,
            shipment.TrackingCode.Value,
            shipment.Status,
            AutoAssignShipmentStatus.Skipped,
            null,
            reason);
    }
}
