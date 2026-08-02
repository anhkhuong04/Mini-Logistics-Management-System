using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Notifications;

public sealed class ShopNotificationService : IShopNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IShopAccessService _shopAccessService;
    private readonly IShopNotificationRepository _notificationRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShopNotificationService> _logger;

    public ShopNotificationService(
        IShopAccessService shopAccessService,
        IShopNotificationRepository notificationRepository,
        IOutboxWriter outboxWriter,
        TimeProvider timeProvider,
        ILogger<ShopNotificationService> logger)
    {
        _shopAccessService = shopAccessService;
        _notificationRepository = notificationRepository;
        _outboxWriter = outboxWriter;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task QueueShipmentEventAsync(
        Shipment shipment,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (!ShopNotificationEventTypes.All.Contains(eventType, StringComparer.Ordinal))
        {
            return;
        }

        try
        {
            var eventId = Guid.NewGuid();
            var payload = new ShopNotificationOutboxPayload(
                shipment.ShopId,
                shipment.Id,
                shipment.TrackingCode.Value,
                eventType);
            await _outboxWriter.AddAsync(
                new OutboxMessage(
                    eventId,
                    OutboxMessageTypes.ShopNotification,
                    shipment.Id,
                    JsonSerializer.Serialize(payload, JsonOptions),
                    _timeProvider.GetUtcNow()),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not queue shop notification {EventType} for shipment {ShipmentId}.",
                eventType,
                shipment.Id);
        }
    }

    public async Task<Result<IReadOnlyList<ShopNotificationResponse>>> GetAsync(
        Guid currentUserId,
        Guid? shopId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await GetShopAsync(
            currentUserId,
            shopId,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<IReadOnlyList<ShopNotificationResponse>>.Failure(shopResult.Error);
        }

        var notifications = await _notificationRepository.GetRecentAsync(
            shopResult.Value.Shop.Id,
            currentUserId,
            Math.Clamp(limit, 1, 200),
            cancellationToken);
        return Result<IReadOnlyList<ShopNotificationResponse>>.Success(notifications
            .Select(ToResponse)
            .ToList());
    }

    public async Task<Result> MarkReadAsync(
        Guid currentUserId,
        Guid? shopId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await GetShopAsync(
            currentUserId,
            shopId,
            ShopPermission.ViewShipments,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result.Failure(shopResult.Error);
        }

        var notification = await _notificationRepository.GetByIdAsync(
            notificationId,
            shopResult.Value.Shop.Id,
            currentUserId,
            cancellationToken);
        if (notification is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Notification was not found for current shop."));
        }

        notification.MarkRead(_timeProvider.GetUtcNow());
        await _notificationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ShopNotificationPreferenceResponse>> GetPreferencesAsync(
        Guid currentUserId,
        Guid? shopId,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await GetShopAsync(
            currentUserId,
            shopId,
            ShopPermission.ManageNotifications,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShopNotificationPreferenceResponse>.Failure(shopResult.Error);
        }

        var preference = await _notificationRepository.GetPreferenceAsync(
            shopResult.Value.Shop.Id,
            currentUserId,
            cancellationToken);
        return Result<ShopNotificationPreferenceResponse>.Success(new ShopNotificationPreferenceResponse(
            shopResult.Value.Shop.Id,
            preference is null
                ? ShopNotificationEventTypes.All
                : ParseEvents(preference.EnabledEventTypes)));
    }

    public async Task<Result<ShopNotificationPreferenceResponse>> UpdatePreferencesAsync(
        Guid currentUserId,
        Guid? shopId,
        IReadOnlyCollection<string> enabledEventTypes,
        CancellationToken cancellationToken = default)
    {
        var invalidEvents = enabledEventTypes
            .Except(ShopNotificationEventTypes.All, StringComparer.Ordinal)
            .ToList();
        if (invalidEvents.Count > 0)
        {
            return Result<ShopNotificationPreferenceResponse>.Failure(
                ApplicationErrors.ValidationFailed($"Unsupported notification events: {string.Join(", ", invalidEvents)}."));
        }

        var shopResult = await GetShopAsync(
            currentUserId,
            shopId,
            ShopPermission.ManageNotifications,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShopNotificationPreferenceResponse>.Failure(shopResult.Error);
        }

        var now = _timeProvider.GetUtcNow();
        var preference = await _notificationRepository.GetPreferenceAsync(
            shopResult.Value.Shop.Id,
            currentUserId,
            cancellationToken);
        if (preference is null)
        {
            preference = new ShopNotificationPreference(
                shopResult.Value.Shop.Id,
                currentUserId,
                enabledEventTypes,
                now);
            await _notificationRepository.AddPreferenceAsync(preference, cancellationToken);
        }
        else
        {
            preference.SetEnabledEvents(enabledEventTypes, now);
        }

        await _notificationRepository.SaveChangesAsync(cancellationToken);
        return Result<ShopNotificationPreferenceResponse>.Success(new ShopNotificationPreferenceResponse(
            shopResult.Value.Shop.Id,
            ParseEvents(preference.EnabledEventTypes)));
    }

    private Task<Result<ShopAccessContext>> GetShopAsync(
        Guid currentUserId,
        Guid? shopId,
        ShopPermission requiredPermission,
        CancellationToken cancellationToken)
    {
        return _shopAccessService.GetShopAccessAsync(
            currentUserId,
            shopId,
            requireActiveShop: false,
            requiredPermission,
            cancellationToken);
    }

    private static ShopNotificationResponse ToResponse(ShopNotification notification)
    {
        return new ShopNotificationResponse(
            notification.Id,
            notification.ShopId,
            notification.EventType,
            notification.Title,
            notification.Message,
            notification.ShipmentId,
            notification.IsRead,
            notification.CreatedAtUtc);
    }

    private static IReadOnlyList<string> ParseEvents(string enabledEvents)
    {
        return enabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
