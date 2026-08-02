using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Notifications;

public interface IShopNotificationRepository
{
    Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task AddAsync(ShopNotification notification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopNotification>> GetRecentAsync(
        Guid shopId,
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ShopNotification?> GetByIdAsync(
        Guid notificationId,
        Guid shopId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ShopNotificationPreference?> GetPreferenceAsync(
        Guid shopId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddPreferenceAsync(
        ShopNotificationPreference preference,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
