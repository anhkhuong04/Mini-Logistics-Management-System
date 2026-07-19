using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Domain.Tests;

public sealed class FeeRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AppliesTo_RespectsRouteAndWeightBounds()
    {
        var rule = CreateRule(minimumWeightKg: 1m, maximumWeightKg: 3m);

        Assert.False(rule.AppliesTo(CreateRequest(RouteType.InterRegion, 2m)));
        Assert.False(rule.AppliesTo(CreateRequest(RouteType.IntraProvince, 0.999m)));
        Assert.True(rule.AppliesTo(CreateRequest(RouteType.IntraProvince, 1m)));
        Assert.True(rule.AppliesTo(CreateRequest(RouteType.IntraProvince, 3m)));
        Assert.False(rule.AppliesTo(CreateRequest(RouteType.IntraProvince, 3.001m)));
    }

    [Fact]
    public void Calculate_UsesChargeableWeightAndRoundsExtraWeightBlocks()
    {
        var rule = CreateRule();
        var request = CreateRequest(RouteType.IntraProvince, 1.2m, declaredGoodsValue: 500_000m);

        var quote = rule.Calculate(request);

        Assert.Equal(1.2m, quote.ChargeableWeightKg);
        Assert.Equal(1, quote.ExtraWeightBlocks);
        Assert.Equal(20_000m, quote.BaseFee.Amount);
        Assert.Equal(5_000m, quote.ExtraWeightFee.Amount);
        Assert.Equal(0m, quote.InsuranceFee.Amount);
        Assert.Equal(25_000m, quote.TotalFee.Amount);
    }

    private static FeeRule CreateRule(decimal? minimumWeightKg = null, decimal? maximumWeightKg = null)
    {
        return new FeeRule(
            RouteType.IntraProvince,
            baseWeightKg: 1m,
            baseFee: new Money(20_000m),
            extraWeightStepKg: 0.5m,
            extraStepFee: new Money(5_000m),
            createdAtUtc: Now,
            minimumWeightKg: minimumWeightKg,
            maximumWeightKg: maximumWeightKg);
    }

    private static ShippingFeeRequest CreateRequest(
        RouteType routeType,
        decimal actualWeightKg,
        decimal declaredGoodsValue = 100_000m)
    {
        return new ShippingFeeRequest(
            routeType,
            new Weight(actualWeightKg),
            new ParcelDimensions(10m, 10m, 10m),
            new Money(declaredGoodsValue));
    }
}
