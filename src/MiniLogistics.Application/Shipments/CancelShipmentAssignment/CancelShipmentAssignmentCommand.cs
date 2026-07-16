namespace MiniLogistics.Application.Shipments.CancelShipmentAssignment;

public sealed record CancelShipmentAssignmentCommand(
    Guid ShipmentId,
    Guid CancelledByUserId,
    string Reason);
