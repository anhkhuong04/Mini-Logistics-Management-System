namespace MiniLogistics.Application.Shipments.ExportShopShipments;

public sealed record ExportShopShipmentsCsvResponse(
    string FileName,
    string ContentType,
    byte[] Content);
