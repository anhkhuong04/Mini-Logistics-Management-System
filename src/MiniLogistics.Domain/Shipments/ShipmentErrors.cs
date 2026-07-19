using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

/// <summary>
/// Provides domain helpers or errors for Shipment Errors.
/// </summary>
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

    public static readonly Error CannotReassign = new(
        "Shipment.CannotReassign",
        "Shipment can only be reassigned while assigned and before pickup starts.");

    public static readonly Error CannotCancelAssignment = new(
        "Shipment.CannotCancelAssignment",
        "Shipment assignment can only be cancelled while assigned and before pickup starts.");

    public static readonly Error ActiveAssignmentNotFound = new(
        "Shipment.ActiveAssignmentNotFound",
        "Shipment does not have an active assignment.");

    public static readonly Error CannotEditBeforePickup = new(
        "Shipment.CannotEditBeforePickup",
        "Only draft or pending pickup shipments without an active assignment can be edited.");

    public static readonly Error OnlyDraftCanBeSubmitted = new(
        "Shipment.OnlyDraftCanBeSubmitted",
        "Only draft shipments can be submitted.");

    public static readonly Error CodCollectionRequiresDeliveredShipment = new(
        "Shipment.CodCollectionRequiresDeliveredShipment",
        "Shipment assignments can only be closed after COD collection for delivered shipments.");
}
