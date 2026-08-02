using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shops.Notifications;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShopNotificationRepository : IShopNotificationRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShopNotificationRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        _dbContext.ShopNotifications.AnyAsync(notification => notification.Id == notificationId, cancellationToken);

    public async Task AddAsync(ShopNotification notification, CancellationToken cancellationToken = default) =>
        await _dbContext.ShopNotifications.AddAsync(notification, cancellationToken);

    public async Task<IReadOnlyList<ShopNotification>> GetRecentAsync(
        Guid shopId,
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ShopNotifications
            .AsNoTracking()
            .Where(notification => notification.ShopId == shopId && notification.UserId == userId)
            .OrderBy(notification => notification.IsRead)
            .ThenByDescending(notification => notification.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<ShopNotification?> GetByIdAsync(
        Guid notificationId,
        Guid shopId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ShopNotifications.FirstOrDefaultAsync(notification =>
            notification.Id == notificationId
            && notification.ShopId == shopId
            && notification.UserId == userId,
            cancellationToken);

    public Task<ShopNotificationPreference?> GetPreferenceAsync(
        Guid shopId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ShopNotificationPreferences.FirstOrDefaultAsync(preference =>
            preference.ShopId == shopId && preference.UserId == userId,
            cancellationToken);

    public async Task AddPreferenceAsync(
        ShopNotificationPreference preference,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ShopNotificationPreferences.AddAsync(preference, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
