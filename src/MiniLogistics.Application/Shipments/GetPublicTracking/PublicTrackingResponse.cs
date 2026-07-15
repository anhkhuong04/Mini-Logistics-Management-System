using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

public sealed record PublicTrackingResponse(
    string TrackingCode,
    PublicTrackingAccessLevel AccessLevel,
    ShipmentStatus CurrentStatus,
    string PickupProvince,
    string DeliveryProvince,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    string? SenderName,
    string? SenderPhone,
    string? ReceiverName,
    string? ReceiverPhone,
    PublicTrackingAddressResponse? PickupAddress,
    PublicTrackingAddressResponse? DeliveryAddress,
    IReadOnlyList<PublicTrackingTimelineItemResponse> Timeline);

public enum PublicTrackingAccessLevel
{
    Summary = 1,
    Verified = 2
}

public sealed record PublicTrackingAddressResponse(
    string Street,
    string Ward,
    string Province,
    string Country,
    string FullAddress);

public sealed record PublicTrackingTimelineItemResponse(
    ShipmentStatus Status,
    DateTimeOffset ChangedAtUtc);
