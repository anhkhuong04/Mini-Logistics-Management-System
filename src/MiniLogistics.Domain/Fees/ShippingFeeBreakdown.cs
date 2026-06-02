using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

public sealed record ShippingFeeBreakdown
{
    private ShippingFeeBreakdown()
    {
        BaseFee = Money.Zero;
        ExtraWeightFee = Money.Zero;
        InsuranceFee = Money.Zero;
        ReturnFee = Money.Zero;
        TotalFee = Money.Zero;
    }

    public ShippingFeeBreakdown(
        Money baseFee,
        Money extraWeightFee,
        Money insuranceFee,
        Money returnFee)
    {
        ArgumentNullException.ThrowIfNull(baseFee);
        ArgumentNullException.ThrowIfNull(extraWeightFee);
        ArgumentNullException.ThrowIfNull(insuranceFee);
        ArgumentNullException.ThrowIfNull(returnFee);

        BaseFee = baseFee;
        ExtraWeightFee = extraWeightFee;
        InsuranceFee = insuranceFee;
        ReturnFee = returnFee;
        TotalFee = baseFee + extraWeightFee + insuranceFee + returnFee;
    }

    public Money BaseFee { get; private set; }

    public Money ExtraWeightFee { get; private set; }

    public Money ServiceFee => BaseFee + ExtraWeightFee;

    public Money InsuranceFee { get; private set; }

    public Money ReturnFee { get; private set; }

    public Money TotalFee { get; private set; }

    public Money CalculateReturnFee()
    {
        return ServiceFee * 0.5m;
    }

    public ShippingFeeBreakdown WithCalculatedReturnFee()
    {
        return new ShippingFeeBreakdown(
            BaseFee,
            ExtraWeightFee,
            InsuranceFee,
            CalculateReturnFee());
    }
}
