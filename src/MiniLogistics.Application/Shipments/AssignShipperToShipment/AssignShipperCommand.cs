namespace MiniLogistics.Application.Shipments.AssignShipperToShipment;

public sealed record AssignShipperCommand(
    Guid ShipmentId,
    Guid ShipperId,
    Guid AssignedByUserId,
    string? Note);
