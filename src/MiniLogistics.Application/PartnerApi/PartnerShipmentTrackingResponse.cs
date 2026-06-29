using System.Text.Json.Serialization;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerShipmentTrackingResponse(
    string TrackingCode,
    string ExternalOrderId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
    ShipmentStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CodStatus>))]
    CodStatus CodStatus,
    decimal ShippingFeeAmount,
    string Currency,
    IReadOnlyList<PartnerShipmentTimelineItem> Timeline);

public sealed record PartnerShipmentTimelineItem(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
    ShipmentStatus Status,
    string Note,
    DateTimeOffset ChangedAtUtc);
