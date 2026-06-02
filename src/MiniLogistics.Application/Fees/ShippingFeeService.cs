using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Fees;

public sealed class ShippingFeeService : IShippingFeeService
{
    private readonly IFeeRuleRepository _feeRuleRepository;

    public ShippingFeeService(IFeeRuleRepository feeRuleRepository)
    {
        _feeRuleRepository = feeRuleRepository;
    }

    public async Task<Result<ShippingFeeQuote>> CalculateAsync(
        RouteType routeType,
        Weight actualWeight,
        ParcelDimensions dimensions,
        CancellationToken cancellationToken = default)
    {
        return await CalculateAsync(
            routeType,
            actualWeight,
            dimensions,
            Money.Zero,
            cancellationToken);
    }

    public async Task<Result<ShippingFeeQuote>> CalculateAsync(
        RouteType routeType,
        Weight actualWeight,
        ParcelDimensions dimensions,
        Money declaredGoodsValue,
        CancellationToken cancellationToken = default)
    {
        var feeRules = await _feeRuleRepository.GetActiveRulesAsync(routeType, cancellationToken);
        var request = new ShippingFeeRequest(routeType, actualWeight, dimensions, declaredGoodsValue);

        return ShippingFeeCalculator.Calculate(feeRules, request);
    }
}
