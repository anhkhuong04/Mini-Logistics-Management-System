using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Domain.Fees;

namespace MiniLogistics.Application.Fees;

public interface IShippingFeeService
{
    Task<Result<ShippingFeeQuote>> CalculateAsync(
        RouteType routeType,
        Weight actualWeight,
        ParcelDimensions dimensions,
        CancellationToken cancellationToken = default);

    Task<Result<ShippingFeeQuote>> CalculateAsync(
        RouteType routeType,
        Weight actualWeight,
        ParcelDimensions dimensions,
        Money declaredGoodsValue,
        CancellationToken cancellationToken = default);
}
