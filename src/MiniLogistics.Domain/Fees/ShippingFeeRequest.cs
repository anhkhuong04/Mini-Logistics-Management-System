using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

public sealed record ShippingFeeRequest(
    RouteType RouteType,
    Weight ActualWeight,
    ParcelDimensions Dimensions,
    Money DeclaredGoodsValue)
{
    public decimal VolumetricWeightKg => Dimensions.CalculateVolumetricWeightKg();

    public decimal ChargeableWeightKg => Math.Max(ActualWeight.Kilograms, VolumetricWeightKg);

    public Weight ChargeableWeight => new(ChargeableWeightKg);
}
