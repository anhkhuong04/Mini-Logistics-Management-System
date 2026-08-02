using MiniLogistics.Domain.AdminAuditing;

namespace MiniLogistics.Application.Shops.Audit;

public interface IShopAuditLogRepository
{
    Task<IReadOnlyList<AdminAuditLog>> QueryForShopsAsync(
        Guid currentUserId,
        IReadOnlyCollection<Guid> shopIds,
        ShopAuditLogQuery query,
        CancellationToken cancellationToken = default);
}
