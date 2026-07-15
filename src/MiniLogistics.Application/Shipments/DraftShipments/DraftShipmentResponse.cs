using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.DraftShipments;

public sealed record DraftShipmentResponse(
    Guid ShipmentId,
    string TrackingCode,
    decimal WeightKg,
    decimal VolumetricWeightKg,
    decimal ChargeableWeightKg,
    decimal BaseFeeAmount,
    decimal ExtraWeightFeeAmount,
    decimal InsuranceFeeAmount,
    decimal ReturnFeeAmount,
    decimal ShippingFeeAmount,
    string Currency,
    ShipmentStatus Status,
    decimal? PreviousShippingFeeAmount = null)
{
    public bool FeeChanged => PreviousShippingFeeAmount.HasValue
        && PreviousShippingFeeAmount.Value != ShippingFeeAmount;
}
