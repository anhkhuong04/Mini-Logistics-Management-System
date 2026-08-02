using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shops;

public sealed class ShopNotificationPreference : AuditableEntity
{
    private ShopNotificationPreference()
    {
        EnabledEventTypes = string.Empty;
    }

    public ShopNotificationPreference(
        Guid shopId,
        Guid userId,
        IEnumerable<string> enabledEventTypes,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (shopId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Notification preference shop id and user id are required.");
        }

        ShopId = shopId;
        UserId = userId;
        SetEnabledEvents(enabledEventTypes, createdAtUtc);
        UpdatedAtUtc = null;
    }

    public Guid ShopId { get; private set; }

    public Guid UserId { get; private set; }

    public string EnabledEventTypes { get; private set; } = string.Empty;

    public bool IsEnabled(string eventType)
    {
        return EnabledEventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(eventType, StringComparer.Ordinal);
    }

    public void SetEnabledEvents(IEnumerable<string> enabledEventTypes, DateTimeOffset updatedAtUtc)
    {
        EnabledEventTypes = string.Join(',', enabledEventTypes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal));
        MarkUpdated(updatedAtUtc);
    }
}
