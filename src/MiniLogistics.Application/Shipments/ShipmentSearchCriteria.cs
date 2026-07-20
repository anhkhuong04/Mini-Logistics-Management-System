using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

public sealed record PendingPickupShipmentSearchCriteria(
    string? SearchText,
    string? Province,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    decimal? MinCodAmount,
    decimal? MaxCodAmount,
    DateTimeOffset? SlaCutoffUtc,
    int PageNumber,
    int PageSize);

public sealed record OperationsShipmentSearchCriteria(
    IReadOnlyCollection<ShipmentStatus> Statuses,
    string? SearchText,
    CodStatus? CodStatus,
    Guid? ShipperId,
    string? Province,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    decimal? MinCodAmount,
    decimal? MaxCodAmount,
    bool SlaOnly,
    DateTimeOffset SlaReferenceUtc,
    int PageNumber,
    int PageSize);

public sealed record AssignedShipmentsForShipperSearchCriteria(
    Guid ShipperId,
    IReadOnlyCollection<ShipmentStatus>? Statuses,
    string? SearchText,
    CodStatus? CodStatus,
    int PageNumber,
    int PageSize);

public sealed record ShopShipmentSearchCriteria(
    Guid ShopId,
    ShipmentStatus? StatusFilter,
    string? TrackingCodeSearch,
    string? ReceiverNameSearch,
    string? ReceiverPhoneSearch,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    decimal? MinCodAmount,
    decimal? MaxCodAmount,
    ShopShipmentSortBy SortBy,
    SortDirection SortDirection,
    int PageNumber,
    int PageSize);

public enum ShopShipmentSortBy
{
    CreatedAt = 1,
    TrackingCode = 2,
    ReceiverName = 3,
    CodAmount = 4,
    ShippingFee = 5
}

public enum SortDirection
{
    Descending = 1,
    Ascending = 2
}
