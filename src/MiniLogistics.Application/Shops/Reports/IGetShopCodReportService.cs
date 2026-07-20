using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.Reports;

public interface IGetShopCodReportService
{
    Task<Result<ShopCodReportResponse>> GetAsync(
        GetShopCodReportQuery query,
        CancellationToken cancellationToken = default);
}
