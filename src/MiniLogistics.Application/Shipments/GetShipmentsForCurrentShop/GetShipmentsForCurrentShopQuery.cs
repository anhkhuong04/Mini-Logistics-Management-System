using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public sealed record GetShipmentsForCurrentShopQuery(
    Guid OwnerUserId,
    Guid? ShopId = null,
    ShipmentStatus? StatusFilter = null,
    string? TrackingCodeSearch = null,
    string? ReceiverNameSearch = null,
    string? ReceiverPhoneSearch = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    decimal? MinCodAmount = null,
    decimal? MaxCodAmount = null,
    ShopShipmentSortBy SortBy = ShopShipmentSortBy.CreatedAt,
    SortDirection SortDirection = SortDirection.Descending,
    int PageNumber = 1,
    int PageSize = 25);
