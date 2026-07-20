using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ExportShopShipments;

public sealed record ExportShopShipmentsCsvCommand(
    Guid OwnerUserId,
    Guid? ShopId = null,
    ShipmentStatus? StatusFilter = null,
    string? TrackingCodeSearch = null,
    string? ReceiverNameSearch = null,
    string? ReceiverPhoneSearch = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    decimal? MinCodAmount = null,
    decimal? MaxCodAmount = null);
