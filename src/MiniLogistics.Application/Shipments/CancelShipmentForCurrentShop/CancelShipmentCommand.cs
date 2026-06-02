namespace MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;

public sealed record CancelShipmentCommand(
    Guid OwnerUserId,
    Guid ShipmentId,
    string Reason);
