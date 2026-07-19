using System.Security.Cryptography;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

/// <summary>
/// Represents the validated Tracking Code value used by the domain model.
/// </summary>
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

    public static TrackingCode Generate(DateTimeOffset generatedAtUtc)
    {
        var datePart = generatedAtUtc.ToString("yyyyMMddHHmmss");
        var randomPart = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

        return new TrackingCode($"{Prefix}{datePart}{randomPart}");
    }

    public override string ToString() => Value;
}
