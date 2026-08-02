using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.Audit;

public interface IGetShopAuditLogsService
{
    Task<Result<IReadOnlyList<ShopAuditLogResponse>>> GetAsync(
        ShopAuditLogQuery query,
        CancellationToken cancellationToken = default);
}
