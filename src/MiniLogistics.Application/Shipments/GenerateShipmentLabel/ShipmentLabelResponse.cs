namespace MiniLogistics.Application.Shipments.GenerateShipmentLabel;

public sealed record ShipmentLabelResponse(
    string FileName,
    string ContentType,
    byte[] Content);
