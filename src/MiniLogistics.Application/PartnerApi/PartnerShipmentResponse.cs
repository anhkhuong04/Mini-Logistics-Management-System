using System.Text.Json.Serialization;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.PartnerApi;

public sealed record PartnerShipmentResponse(
    Guid ShipmentId,
    string ExternalOrderId,
    string TrackingCode,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
    ShipmentStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RouteType>))]
    RouteType RouteType,
    decimal ShippingFeeAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc);

public sealed record PartnerCreateShipmentResult(
    PartnerShipmentResponse Shipment,
    bool IsIdempotentReplay);
