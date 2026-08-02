using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShopStaffMembershipRepository : IShopStaffMembershipRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShopStaffMembershipRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ShopStaffMembership>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ShopStaffMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShopStaffMembership>> GetByShopIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ShopStaffMemberships
            .Where(membership => membership.ShopId == shopId)
            .OrderByDescending(membership => membership.IsActive)
            .ThenBy(membership => membership.Role)
            .ToListAsync(cancellationToken);

    public Task<ShopStaffMembership?> GetByShopAndUserAsync(
        Guid shopId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ShopStaffMemberships.FirstOrDefaultAsync(
            membership => membership.ShopId == shopId && membership.UserId == userId,
            cancellationToken);

    public async Task AddAsync(
        ShopStaffMembership membership,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ShopStaffMemberships.AddAsync(membership, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
