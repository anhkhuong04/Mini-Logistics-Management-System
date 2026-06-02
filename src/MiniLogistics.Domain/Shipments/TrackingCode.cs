using System.Security.Cryptography;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

public sealed record TrackingCode
{
    private const string Prefix = "ML";

    public TrackingCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Tracking code is required.");
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public string Value { get; }

    public static TrackingCode Generate()
    {
        var datePart = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var randomPart = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

        return new TrackingCode($"{Prefix}{datePart}{randomPart}");
    }

    public override string ToString() => Value;
}
