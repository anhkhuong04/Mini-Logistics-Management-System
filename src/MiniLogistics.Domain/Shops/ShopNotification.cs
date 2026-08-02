using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shops;

public sealed class ShopNotification : AuditableEntity
{
    private ShopNotification()
    {
        EventType = string.Empty;
        Title = string.Empty;
        Message = string.Empty;
    }

    public ShopNotification(
        Guid id,
        Guid shopId,
        Guid userId,
        string eventType,
        string title,
        string message,
        DateTimeOffset createdAtUtc,
        Guid? shipmentId = null)
        : base(id, createdAtUtc)
    {
        if (id == Guid.Empty || shopId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Notification id, shop id and user id are required.");
        }

        ShopId = shopId;
        UserId = userId;
        EventType = DomainGuard.RequireText(eventType, nameof(eventType), 80);
        Title = DomainGuard.RequireText(title, nameof(title), 200);
        Message = DomainGuard.RequireText(message, nameof(message), 500);
        ShipmentId = shipmentId;
    }

    public Guid ShopId { get; private set; }

    public Guid UserId { get; private set; }

    public string EventType { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public Guid? ShipmentId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public void MarkRead(DateTimeOffset readAtUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc;
        MarkUpdated(readAtUtc);
    }
}
