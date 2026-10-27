using FluentValidation.Results;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Web.Endpoints.Browser;

// Browser ingress bounds mirror current SQL decimal precision; legacy/Partner rules are unchanged.
// Domain value objects retain their existing rounding behavior.
internal static class BrowserShipmentStorageLimits
{
    public static IReadOnlyList<ValidationFailure> Validate(decimal weight, decimal length, decimal width, decimal height, decimal goods, decimal cod = 0)
    {
        List<ValidationFailure> errors = [];
        void Check(string field, decimal value, int scale, decimal maximum)
        {
            if (decimal.Round(value, scale) > maximum)
                errors.Add(new(field, "Value is outside the supported range."));
        }
        Check("WeightKg", weight, 3, 9999999.999m);
        if (weight > 0 && decimal.Round(weight, 3) == 0)
            errors.Add(new("WeightKg", "Weight is below the supported precision."));
        Check("LengthCm", length, 2, 99999999.99m);
        Check("WidthCm", width, 2, 99999999.99m);
        Check("HeightCm", height, 2, 99999999.99m);
        Check("GoodsValueAmount", goods, 2, 9999999999999999.99m);
        Check("CodAmount", cod, 2, 9999999999999999.99m);
        if (errors.Count == 0 && length > 0 && width > 0 && height > 0)
        {
            var dimensions = new ParcelDimensions(length, width, height);
            if (dimensions.CalculateVolumetricWeightKg() > 9999999.999m)
                errors.Add(new("LengthCm", "Parcel dimensions exceed the supported chargeable weight."));
        }
        return errors;
    }
}
