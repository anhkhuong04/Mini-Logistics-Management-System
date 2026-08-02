using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shops.Notifications;

public interface IShopNotificationService
{
    Task QueueShipmentEventAsync(
        Shipment shipment,
        string eventType,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ShopNotificationResponse>>> GetAsync(
        Guid currentUserId,
        Guid? shopId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<Result> MarkReadAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<Result<ShopNotificationPreferenceResponse>> GetPreferencesAsync(
        Guid currentUserId,
        Guid? shopId,
        CancellationToken cancellationToken = default);

    Task<Result<ShopNotificationPreferenceResponse>> UpdatePreferencesAsync(
        Guid currentUserId,
        Guid? shopId,
        IReadOnlyCollection<string> enabledEventTypes,
        CancellationToken cancellationToken = default);
}

public sealed class NullShopNotificationService : IShopNotificationService
{
    public static readonly NullShopNotificationService Instance = new();

    private NullShopNotificationService()
    {
    }

    public Task QueueShipmentEventAsync(Shipment shipment, string eventType, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Result<IReadOnlyList<ShopNotificationResponse>>> GetAsync(Guid currentUserId, Guid? shopId, int limit = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<IReadOnlyList<ShopNotificationResponse>>.Success([]));

    public Task<Result> MarkReadAsync(Guid currentUserId, Guid? shopId, Guid notificationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public Task<Result<ShopNotificationPreferenceResponse>> GetPreferencesAsync(Guid currentUserId, Guid? shopId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<ShopNotificationPreferenceResponse>.Failure(Common.ApplicationErrors.NotFound("Notification preferences were not found.")));

    public Task<Result<ShopNotificationPreferenceResponse>> UpdatePreferencesAsync(Guid currentUserId, Guid? shopId, IReadOnlyCollection<string> enabledEventTypes, CancellationToken cancellationToken = default) =>
        GetPreferencesAsync(currentUserId, shopId, cancellationToken);
}
