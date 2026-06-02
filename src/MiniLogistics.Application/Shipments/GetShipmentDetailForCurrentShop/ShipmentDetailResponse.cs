using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

public sealed record ShipmentDetailResponse(
    Guid ShipmentId,
    string TrackingCode,
    string SenderName,
    string SenderPhone,
    string ReceiverName,
    string ReceiverPhone,
    ShipmentAddressResponse PickupAddress,
    ShipmentAddressResponse DeliveryAddress,
    decimal WeightKg,
    decimal ParcelLengthCm,
    decimal ParcelWidthCm,
    decimal ParcelHeightCm,
    decimal VolumetricWeightKg,
    decimal ChargeableWeightKg,
    decimal GoodsValueAmount,
    decimal CodAmount,
    decimal BaseFeeAmount,
    decimal ExtraWeightFeeAmount,
    decimal InsuranceFeeAmount,
    decimal ReturnFeeAmount,
    decimal ShippingFeeAmount,
    string Currency,
    RouteType RouteType,
    string? Note,
    ShipmentStatus Status,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ShipmentStatusHistoryResponse> TrackingHistory);
