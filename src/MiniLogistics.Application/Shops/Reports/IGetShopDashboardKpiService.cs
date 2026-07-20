using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.Reports;

public interface IGetShopDashboardKpiService
{
    Task<Result<ShopDashboardKpiResponse>> GetAsync(
        ShopDashboardKpiQuery query,
        CancellationToken cancellationToken = default);
}
