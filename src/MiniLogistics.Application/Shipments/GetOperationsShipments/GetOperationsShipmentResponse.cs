using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

public sealed record GetOperationsShipmentResponse(
    Guid ShipmentId,
    string TrackingCode,
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
    Guid? ActiveShipperId,
    string? ActiveShipperName,
    string? ActiveShipperPhone,
    IReadOnlyList<ShipmentStatusHistoryResponse> TrackingHistory);
