namespace MiniLogistics.Application.Shipments.GenerateShipmentLabel;

public sealed record GenerateShipmentLabelCommand(
    Guid OwnerUserId,
    Guid ShipmentId,
    Guid? ShopId = null);
