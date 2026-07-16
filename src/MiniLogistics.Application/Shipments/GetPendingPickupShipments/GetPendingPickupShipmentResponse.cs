namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public sealed record GetPendingPickupShipmentResponse(
    Guid ShipmentId,
    string TrackingCode,
    string ReceiverName,
    string PickupProvince,
    string DeliveryProvince,
    decimal CodAmount,
    decimal ShippingFeeAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    bool IsSlaOverdue = false);
