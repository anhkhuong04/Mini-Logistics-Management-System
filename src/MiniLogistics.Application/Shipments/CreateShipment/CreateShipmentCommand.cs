namespace MiniLogistics.Application.Shipments.CreateShipment;

public sealed record CreateShipmentCommand(
    Guid CreatedByUserId,
    string SenderName,
    string SenderPhone,
    string ReceiverName,
    string ReceiverPhone,
    ShipmentAddressDto PickupAddress,
    ShipmentAddressDto DeliveryAddress,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string Currency = "VND",
    string? Note = null,
    Guid? ShopId = null);
