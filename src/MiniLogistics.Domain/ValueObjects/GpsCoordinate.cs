using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

public sealed record GpsCoordinate
{
    public GpsCoordinate(
        decimal latitude,
        decimal longitude,
        decimal? accuracyMeters = null,
        DateTimeOffset? capturedAtUtc = null)
    {
        if (latitude is < -90 or > 90)
        {
            throw new DomainException("GPS latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException("GPS longitude must be between -180 and 180.");
        }

        if (accuracyMeters.HasValue && accuracyMeters.Value < 0)
        {
            throw new DomainException("GPS accuracy cannot be negative.");
        }

        Latitude = decimal.Round(latitude, 6);
        Longitude = decimal.Round(longitude, 6);
        AccuracyMeters = accuracyMeters.HasValue
            ? decimal.Round(accuracyMeters.Value, 2)
            : null;
        CapturedAtUtc = capturedAtUtc;
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    public decimal? AccuracyMeters { get; }

    public DateTimeOffset? CapturedAtUtc { get; }
}
