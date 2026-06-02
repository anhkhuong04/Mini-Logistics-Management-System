using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Fees;

public static class FeeRuleErrors
{
    public static readonly Error NoMatchingRule = new(
        "FeeRule.NoMatchingRule",
        "No active fee rule matches the shipment route and weight.");
}
