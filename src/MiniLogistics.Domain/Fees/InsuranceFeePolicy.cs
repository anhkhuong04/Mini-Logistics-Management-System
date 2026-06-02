using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

public static class InsuranceFeePolicy
{
    public const decimal FreeInsuranceThreshold = 1_000_000m;
    public const decimal MaximumInsuredValue = 20_000_000m;
    public const decimal InsuranceRate = 0.005m;

    public static Money Calculate(Money declaredGoodsValue)
    {
        if (declaredGoodsValue.Amount < FreeInsuranceThreshold)
        {
            return new Money(0, declaredGoodsValue.Currency);
        }

        var insuredValue = Math.Min(declaredGoodsValue.Amount, MaximumInsuredValue);
        return new Money(insuredValue * InsuranceRate, declaredGoodsValue.Currency);
    }
}
