using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

/// <summary>
/// Represents the validated Shipping Fee Breakdown value used by the domain model.
/// </summary>
public sealed record ShippingFeeBreakdown
{
    private ShippingFeeBreakdown()
    {
        BaseFee = Money.Zero;
        ExtraWeightFee = Money.Zero;
        InsuranceFee = Money.Zero;
        ReturnFee = Money.Zero;
        TotalFee = Money.Zero;
        ReturnFeeRate = 0.5m;
    }

    public ShippingFeeBreakdown(
        Money baseFee,
        Money extraWeightFee,
        Money insuranceFee,
        Money returnFee,
        decimal returnFeeRate = 0.5m)
    {
        ArgumentNullException.ThrowIfNull(baseFee);
        ArgumentNullException.ThrowIfNull(extraWeightFee);
        ArgumentNullException.ThrowIfNull(insuranceFee);
        ArgumentNullException.ThrowIfNull(returnFee);
        if (returnFeeRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returnFeeRate), "Return fee rate cannot be negative.");
        }

        BaseFee = baseFee;
        ExtraWeightFee = extraWeightFee;
        InsuranceFee = insuranceFee;
        ReturnFee = returnFee;
        ReturnFeeRate = decimal.Round(returnFeeRate, 4);
        TotalFee = baseFee + extraWeightFee + insuranceFee + returnFee;
    }

    public Money BaseFee { get; private set; }

    public Money ExtraWeightFee { get; private set; }

    public Money ServiceFee => BaseFee + ExtraWeightFee;

    public Money InsuranceFee { get; private set; }

    public Money ReturnFee { get; private set; }

    public decimal ReturnFeeRate { get; private set; }

    public Money TotalFee { get; private set; }

    public Money CalculateReturnFee()
    {
        return ServiceFee * ReturnFeeRate;
    }

    public ShippingFeeBreakdown WithCalculatedReturnFee()
    {
        return new ShippingFeeBreakdown(
            BaseFee,
            ExtraWeightFee,
            InsuranceFee,
            CalculateReturnFee(),
            ReturnFeeRate);
    }
}
