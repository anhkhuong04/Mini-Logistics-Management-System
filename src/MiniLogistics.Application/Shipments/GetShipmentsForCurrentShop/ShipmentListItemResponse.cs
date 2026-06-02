using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed record ShipmentListItemResponse(
    Guid ShipmentId,
    string TrackingCode,
    string ReceiverName,
    RouteType RouteType,
    decimal WeightKg,
    decimal ChargeableWeightKg,
    decimal CodAmount,
    decimal ShippingFeeAmount,
    string Currency,
    ShipmentStatus Status,
    DateTimeOffset CreatedAtUtc);
