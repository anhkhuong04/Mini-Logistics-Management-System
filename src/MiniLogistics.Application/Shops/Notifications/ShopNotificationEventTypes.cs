namespace MiniLogistics.Application.Shops.Notifications;

public static class ShopNotificationEventTypes
{
    public const string Assigned = "shipment.assigned";
    public const string PickedUp = "shipment.picked_up";
    public const string Delivered = "shipment.delivered";
    public const string DeliveryFailed = "shipment.delivery_failed";
    public const string Returned = "shipment.returned";
    public const string CodCollected = "cod.collected";
    public const string CodSettled = "cod.settled";

    public static readonly IReadOnlyList<string> All =
    [
        Assigned, PickedUp, Delivered, DeliveryFailed, Returned, CodCollected, CodSettled
    ];
}
