using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.ValueObjects;

public sealed record ParcelDimensions
{
    public const decimal DefaultVolumetricDivisor = 5000m;

    public ParcelDimensions(
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm)
    {
        if (lengthCm <= 0)
        {
            throw new DomainException("Parcel length must be greater than zero.");
        }

        if (widthCm <= 0)
        {
            throw new DomainException("Parcel width must be greater than zero.");
        }

        if (heightCm <= 0)
        {
            throw new DomainException("Parcel height must be greater than zero.");
        }

        LengthCm = decimal.Round(lengthCm, 2);
        WidthCm = decimal.Round(widthCm, 2);
        HeightCm = decimal.Round(heightCm, 2);
    }

    public decimal LengthCm { get; }

    public decimal WidthCm { get; }

    public decimal HeightCm { get; }

    public decimal CalculateVolumetricWeightKg(decimal divisor = DefaultVolumetricDivisor)
    {
        if (divisor <= 0)
        {
            throw new DomainException("Volumetric divisor must be greater than zero.");
        }

        return decimal.Round(LengthCm * WidthCm * HeightCm / divisor, 3);
    }
}
