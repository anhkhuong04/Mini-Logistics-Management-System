using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.CreateShipment;

public sealed record CreateShipmentResponse(
    Guid ShipmentId,
    string TrackingCode,
    decimal ActualWeightKg,
    decimal VolumetricWeightKg,
    decimal ChargeableWeightKg,
    decimal BaseFeeAmount,
    decimal ExtraWeightFeeAmount,
    decimal InsuranceFeeAmount,
    decimal ReturnFeeAmount,
    decimal ShippingFeeAmount,
    string Currency,
    ShipmentStatus Status);
