namespace MiniLogistics.Application.Shipments.ImportShipments;

public sealed record PreviewShipmentImportCommand(
    Guid CurrentUserId,
    Guid? ShopId,
    string CsvContent);

public sealed record ConfirmShipmentImportCommand(
    Guid CurrentUserId,
    Guid? ShopId,
    IReadOnlyList<ShipmentImportRowDraft> Rows);

public sealed record ShipmentImportRowDraft(
    int RowNumber,
    string? ClientOrderCode,
    string ReceiverName,
    string ReceiverPhone,
    string DeliveryStreet,
    string DeliveryWard,
    string DeliveryProvince,
    string DeliveryCountry,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string? Note);
