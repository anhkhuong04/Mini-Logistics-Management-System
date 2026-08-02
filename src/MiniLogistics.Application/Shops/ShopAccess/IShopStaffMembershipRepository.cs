using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.ShopAccess;

public interface IShopStaffMembershipRepository
{
    Task<IReadOnlyList<ShopStaffMembership>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopStaffMembership>> GetByShopIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default);

    Task<ShopStaffMembership?> GetByShopAndUserAsync(
        Guid shopId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ShopStaffMembership membership,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
