namespace MiniLogistics.Application.Shipments.ReassignShipment;

public sealed record ReassignShipmentCommand(
    Guid ShipmentId,
    Guid NewShipperId,
    Guid ReassignedByUserId,
    string Reason);
