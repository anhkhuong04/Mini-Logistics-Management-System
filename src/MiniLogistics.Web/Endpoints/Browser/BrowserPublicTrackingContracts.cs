using MiniLogistics.Application.Shipments.GetPublicTracking;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserPublicTrackingResponse(
    string TrackingCode,
    PublicTrackingAccessLevel AccessLevel,
    ShipmentStatus CurrentStatus,
    string PickupProvince,
    string DeliveryProvince,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    string? SenderName,
    string? ReceiverName,
    IReadOnlyList<PublicTrackingTimelineItemResponse> Timeline,
    string ServerTimeZoneId);
