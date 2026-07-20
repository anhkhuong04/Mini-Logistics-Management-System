namespace MiniLogistics.Application.Shipments;

public sealed record GpsCoordinateDto(
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters = null,
    DateTimeOffset? CapturedAtUtc = null);
