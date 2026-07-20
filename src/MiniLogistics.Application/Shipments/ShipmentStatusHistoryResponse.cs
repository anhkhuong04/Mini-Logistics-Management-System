using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

public sealed record ShipmentStatusHistoryResponse(
    ShipmentStatus Status,
    string Note,
    DateTimeOffset ChangedAtUtc,
    Guid ChangedByUserId,
    string ChangedByDisplayName,
    string? ChangedByEmail,
    bool ChangedByUserFound,
    FailureReasonCode? FailureReasonCode = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    decimal? GpsAccuracyMeters = null,
    DateTimeOffset? GpsCapturedAtUtc = null);
