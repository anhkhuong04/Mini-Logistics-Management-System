using MiniLogistics.Application.Shipments.CreateShipment;

namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerCreateShipmentCommand(
    Guid ApiClientId,
    Guid ShopId,
    string ExternalOrderId,
    string IdempotencyKey,
    string? SenderName,
    string? SenderPhone,
    string ReceiverName,
    string ReceiverPhone,
    ShipmentAddressDto? PickupAddress,
    ShipmentAddressDto DeliveryAddress,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string Currency = "VND",
    string? Note = null);
