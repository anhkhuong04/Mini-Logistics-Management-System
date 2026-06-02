using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

public sealed record PublicTrackingResponse(
    string TrackingCode,
    ShipmentStatus CurrentStatus,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ShipmentStatusHistoryResponse> Timeline);
