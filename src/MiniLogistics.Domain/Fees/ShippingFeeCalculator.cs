using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Fees;

public static class ShippingFeeCalculator
{
    public static Result<ShippingFeeQuote> Calculate(
        IEnumerable<FeeRule> feeRules,
        ShippingFeeRequest request)
    {
        var feeRule = feeRules.FirstOrDefault(rule => rule.AppliesTo(request));

        return feeRule is null
            ? Result<ShippingFeeQuote>.Failure(FeeRuleErrors.NoMatchingRule)
            : Result<ShippingFeeQuote>.Success(feeRule.Calculate(request));
    }
}
