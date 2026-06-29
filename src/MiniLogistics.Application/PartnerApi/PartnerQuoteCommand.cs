using MiniLogistics.Application.Shipments.CreateShipment;

namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerQuoteCommand(
    Guid ApiClientId,
    Guid ShopId,
    ShipmentAddressDto? PickupAddress,
    ShipmentAddressDto DeliveryAddress,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal GoodsValueAmount,
    decimal CodAmount,
    string Currency = "VND",
    string? ExternalOrderId = null);
