using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

/// <summary>
/// Represents the validated Shipping Fee Quote value used by the domain model.
/// </summary>
public sealed record ShippingFeeQuote(
    ShippingFeeBreakdown Breakdown,
    decimal ActualWeightKg,
    decimal VolumetricWeightKg,
    decimal ChargeableWeightKg,
    decimal BaseWeightKg,
    decimal ExtraWeightStepKg,
    int ExtraWeightBlocks)
{
    public Money BaseFee => Breakdown.BaseFee;

    public Money ExtraWeightFee => Breakdown.ExtraWeightFee;

    public Money InsuranceFee => Breakdown.InsuranceFee;

    public Money ReturnFee => Breakdown.ReturnFee;

    public Money TotalFee => Breakdown.TotalFee;
}
