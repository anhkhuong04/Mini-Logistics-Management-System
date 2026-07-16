using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetOperationsShipments;

public sealed record GetOperationsShipmentsQuery(
    string? SearchText = null,
    ShipmentStatus? Status = null,
    CodStatus? CodStatus = null,
    Guid? ShipperId = null,
    string? Province = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    decimal? MinCodAmount = null,
    decimal? MaxCodAmount = null,
    bool SlaOnly = false,
    int PageNumber = 1,
    int PageSize = 25);
