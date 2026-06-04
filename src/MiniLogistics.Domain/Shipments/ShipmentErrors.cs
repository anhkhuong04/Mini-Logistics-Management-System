using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

public static class ShipmentErrors
{
    public static readonly Error CannotAssign = new(
        "Shipment.CannotAssign",
        "Only pending pickup shipments can be assigned.");

    public static readonly Error ActiveAssignmentExists = new(
        "Shipment.ActiveAssignmentExists",
        "Shipment already has an active assignment.");

    public static readonly Error InvalidShipper = new(
        "Shipment.InvalidShipper",
        "Shipper id is required.");

    public static readonly Error InvalidStatusTransition = new(
        "Shipment.InvalidStatusTransition",
        "Shipment status transition is not allowed.");

    public static readonly Error DeliveryFailedNoteRequired = new(
        "Shipment.DeliveryFailedNoteRequired",
        "Delivery failure reason is required.");

    public static readonly Error CompletedShipmentCannotChange = new(
        "Shipment.CompletedShipmentCannotChange",
        "Completed shipments cannot be changed.");

    public static readonly Error CannotCancel = new(
        "Shipment.CannotCancel",
        "Shipment cannot be cancelled in its current status.");
}
