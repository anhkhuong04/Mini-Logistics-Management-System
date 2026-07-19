using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed record GetShipmentsForCurrentShopQuery(
    Guid OwnerUserId,
    Guid? ShopId = null,
    ShipmentStatus? StatusFilter = null,
    string? TrackingCodeSearch = null,
    int PageNumber = 1,
    int PageSize = 25);
