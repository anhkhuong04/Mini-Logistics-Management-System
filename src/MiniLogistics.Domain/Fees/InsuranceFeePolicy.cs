using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

public static class InsuranceFeePolicy
{
    public const decimal FreeInsuranceThreshold = 1_000_000m;
    public const decimal MaximumInsuredValue = 20_000_000m;
    public const decimal InsuranceRate = 0.005m;

    public static Money Calculate(Money declaredGoodsValue)
    {
        return Calculate(
            declaredGoodsValue,
            FreeInsuranceThreshold,
            MaximumInsuredValue,
            InsuranceRate);
    }

    public static Money Calculate(
        Money declaredGoodsValue,
        decimal freeInsuranceThreshold,
        decimal maximumInsuredValue,
        decimal insuranceRate)
    {
        if (declaredGoodsValue.Amount < freeInsuranceThreshold)
        {
            return new Money(0, declaredGoodsValue.Currency);
        }

        var insuredValue = Math.Min(declaredGoodsValue.Amount, maximumInsuredValue);
        return new Money(insuredValue * insuranceRate, declaredGoodsValue.Currency);
    }
}
