namespace MiniLogistics.Application.Shipments.GetPendingPickupShipments;

public sealed record GetPendingPickupShipmentsQuery(
    string? SearchText = null,
    string? Province = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    decimal? MinCodAmount = null,
    decimal? MaxCodAmount = null,
    bool SlaOnly = false,
    int PageNumber = 1,
    int PageSize = 25);
