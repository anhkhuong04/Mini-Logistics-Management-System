namespace MiniLogistics.Application.Shops.Notifications;

public sealed record ShopNotificationResponse(
    Guid NotificationId,
    Guid ShopId,
    string EventType,
    string Title,
    string Message,
    Guid? ShipmentId,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);

public sealed record ShopNotificationPreferenceResponse(
    Guid ShopId,
    IReadOnlyList<string> EnabledEventTypes);

public sealed record ShopNotificationOutboxPayload(
    Guid ShopId,
    Guid ShipmentId,
    string TrackingCode,
    string EventType);
