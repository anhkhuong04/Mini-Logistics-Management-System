using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;

public sealed record GetAssignedShipmentForShipperResponse(
    Guid ShipmentId,
    string TrackingCode,
    string SenderName,
    string SenderPhone,
    string ReceiverName,
    string ReceiverPhone,
    ShipmentAddressResponse PickupAddress,
    ShipmentAddressResponse DeliveryAddress,
    decimal CodAmount,
    CodStatus CodStatus,
    decimal ShippingFeeAmount,
    string Currency,
    ShipmentStatus Status,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ShipmentStatusHistoryResponse> TrackingHistory);
