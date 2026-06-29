using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops;

public interface IShopRepository
{
    Task<Shop?> GetByIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default);

    Task<Shop?> GetByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Shop shop, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
