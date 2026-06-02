using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShopRepository : IShopRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShopRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Shop?> GetByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shops
            .FirstOrDefaultAsync(shop => shop.OwnerUserId == ownerUserId, cancellationToken);
    }

    public Task<bool> ExistsByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Shops
            .AnyAsync(shop => shop.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task AddAsync(Shop shop, CancellationToken cancellationToken = default)
    {
        await _dbContext.Shops.AddAsync(shop, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
